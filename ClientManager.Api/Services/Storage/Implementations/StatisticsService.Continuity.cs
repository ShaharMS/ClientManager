using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Storage.Implementations;

/// <summary>
/// Stitches rolled-up and live usage buckets into one continuous history.
/// </summary>
public partial class StatisticsService
{
    private readonly record struct AggregatedBucketTotals(
        long Granted,
        long Denied,
        long Released,
        long Active);

    private sealed class ContinuousBucketState(BucketGranularity requested)
    {
        public SortedDictionary<DateTime, AggregatedBucketTotals> Buckets { get; } = [];
        public BucketGranularity ActualGranularity { get; set; } = requested;
        public bool FoundAny { get; set; }
        public DateTime? EarliestTimestamp { get; set; }
        public DateTime? LatestTimestamp { get; set; }
    }

    private async Task<(List<HistoricalUsagePoint> Points, BucketGranularity ActualGranularity)> GetContinuousHistoryAsync(
        string targetId,
        TargetType targetType,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity requested,
        CancellationToken cancellationToken)
    {
        var clientIds = clientId is null ? null : new[] { clientId };
        var totalsByTarget = await GetContinuousBucketTotalsByTargetAsync(
            [targetId],
            targetType,
            clientIds,
            from,
            to,
            requested,
            cancellationToken);

        if (!totalsByTarget.TryGetValue(targetId, out var result))
        {
            return ([], requested);
        }

        return (ToHistoricalUsagePoints(result.Buckets), result.ActualGranularity);
    }

    private async Task<IReadOnlyDictionary<string, (SortedDictionary<DateTime, AggregatedBucketTotals> Buckets, BucketGranularity ActualGranularity)>> GetContinuousBucketTotalsByTargetAsync(
        IReadOnlyCollection<string> targetIds,
        TargetType targetType,
        IReadOnlyCollection<string>? clientIds,
        DateTime from,
        DateTime to,
        BucketGranularity requested,
        CancellationToken cancellationToken)
    {
        var states = targetIds.ToDictionary(
            targetId => targetId,
            _ => new ContinuousBucketState(requested),
            StringComparer.Ordinal);
        var selectedClientIds = await ResolveClientIdsAsync(clientIds, cancellationToken);

        foreach (var granularity in GetGranularityFallbackOrder(requested))
        {
            var snapshots = await LoadHistorySnapshotsAsync(
                states.Keys.ToList(),
                targetType,
                selectedClientIds,
                from,
                to,
                granularity,
                cancellationToken);
            var aggregatedByTarget = AggregateSnapshotsByTarget(snapshots, from, to);

            foreach (var (targetId, aggregated) in aggregatedByTarget)
            {
                if (states.TryGetValue(targetId, out var state))
                {
                    MergeCandidateBuckets(state, aggregated, requested, granularity);
                }
            }
        }

        return states.ToDictionary(
            kvp => kvp.Key,
            kvp => (kvp.Value.Buckets, kvp.Value.ActualGranularity),
            StringComparer.Ordinal);
    }

    private async Task<IReadOnlyDictionary<(string TargetId, string ClientId), (SortedDictionary<DateTime, AggregatedBucketTotals> Buckets, BucketGranularity ActualGranularity)>> GetContinuousBucketTotalsByTargetClientAsync(
        IReadOnlyCollection<string> targetIds,
        TargetType targetType,
        IReadOnlyCollection<string> clientIds,
        DateTime from,
        DateTime to,
        BucketGranularity requested,
        CancellationToken cancellationToken)
    {
        var selectedClientIds = clientIds.Distinct(StringComparer.Ordinal).ToList();
        var states = new Dictionary<(string TargetId, string ClientId), ContinuousBucketState>();

        foreach (var targetId in targetIds)
        {
            foreach (var clientId in selectedClientIds)
            {
                states[(targetId, clientId)] = new ContinuousBucketState(requested);
            }
        }

        foreach (var granularity in GetGranularityFallbackOrder(requested))
        {
            var snapshots = await LoadHistorySnapshotsAsync(
                targetIds,
                targetType,
                selectedClientIds,
                from,
                to,
                granularity,
                cancellationToken);
            var aggregatedByClient = AggregateSnapshotsByTargetClient(snapshots, from, to);

            foreach (var (key, aggregated) in aggregatedByClient)
            {
                if (states.TryGetValue(key, out var state))
                {
                    MergeCandidateBuckets(state, aggregated, requested, granularity);
                }
            }
        }

        return states.ToDictionary(
            kvp => kvp.Key,
            kvp => (kvp.Value.Buckets, kvp.Value.ActualGranularity));
    }

    private async Task<IReadOnlyList<string>> ResolveClientIdsAsync(
        IReadOnlyCollection<string>? clientIds,
        CancellationToken cancellationToken)
    {
        if (clientIds is not null)
        {
            return clientIds.Distinct(StringComparer.Ordinal).ToList();
        }

        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
        return clients.Select(client => client.Id).Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task<IReadOnlyList<UsageSnapshot>> LoadHistorySnapshotsAsync(
        IReadOnlyCollection<string> targetIds,
        TargetType targetType,
        IReadOnlyCollection<string>? clientIds,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken)
    {
        var selectedClientIds = clientIds?.Distinct(StringComparer.Ordinal).ToList();

        if (selectedClientIds is null)
        {
            var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
            selectedClientIds = clients.Select(client => client.Id).Distinct(StringComparer.Ordinal).ToList();
        }

        if (targetIds.Count == 0 || selectedClientIds.Count == 0)
        {
            return [];
        }

        return await _usageSnapshotDatabase.GetByTargetsAndRangeAsync(
            targetIds,
            targetType,
            granularity,
            from,
            to,
            selectedClientIds,
            cancellationToken);
    }

    private static Dictionary<string, SortedDictionary<DateTime, AggregatedBucketTotals>> AggregateSnapshotsByTarget(
        IReadOnlyList<UsageSnapshot> snapshots,
        DateTime from,
        DateTime to)
    {
        var aggregated = new Dictionary<string, SortedDictionary<DateTime, AggregatedBucketTotals>>(StringComparer.Ordinal);

        foreach (var snapshot in snapshots)
        {
            if (!aggregated.TryGetValue(snapshot.TargetId, out var buckets))
            {
                buckets = [];
                aggregated[snapshot.TargetId] = buckets;
            }

            AddSnapshotBuckets(buckets, snapshot, from, to);
        }

        return aggregated;
    }

    private static Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, AggregatedBucketTotals>> AggregateSnapshotsByTargetClient(
        IReadOnlyList<UsageSnapshot> snapshots,
        DateTime from,
        DateTime to)
    {
        var aggregated = new Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, AggregatedBucketTotals>>();

        foreach (var snapshot in snapshots)
        {
            var key = (snapshot.TargetId, snapshot.ClientId);
            if (!aggregated.TryGetValue(key, out var buckets))
            {
                buckets = [];
                aggregated[key] = buckets;
            }

            AddSnapshotBuckets(buckets, snapshot, from, to);
        }

        return aggregated;
    }

    private static void AddSnapshotBuckets(
        SortedDictionary<DateTime, AggregatedBucketTotals> aggregated,
        UsageSnapshot snapshot,
        DateTime from,
        DateTime to)
    {
        foreach (var bucket in snapshot.Buckets)
        {
            if (bucket.Timestamp < from || bucket.Timestamp > to)
            {
                continue;
            }

            if (aggregated.TryGetValue(bucket.Timestamp, out var existing))
            {
                aggregated[bucket.Timestamp] = new AggregatedBucketTotals(
                    existing.Granted + bucket.GrantedCount,
                    existing.Denied + bucket.DeniedCount,
                    existing.Released + bucket.ReleasedCount,
                    existing.Active + bucket.ActiveCount);
                continue;
            }

            aggregated[bucket.Timestamp] = new AggregatedBucketTotals(
                bucket.GrantedCount,
                bucket.DeniedCount,
                bucket.ReleasedCount,
                bucket.ActiveCount);
        }
    }

    private static void MergeCandidateBuckets(
        ContinuousBucketState state,
        SortedDictionary<DateTime, AggregatedBucketTotals> aggregated,
        BucketGranularity requested,
        BucketGranularity candidate)
    {
        if (aggregated.Count == 0)
        {
            return;
        }

        if (!state.FoundAny)
        {
            state.ActualGranularity = candidate;
            state.FoundAny = true;
        }

        foreach (var entry in FilterContinuityWindow(aggregated, requested, candidate, state.EarliestTimestamp, state.LatestTimestamp))
        {
            if (state.Buckets.ContainsKey(entry.Key))
            {
                continue;
            }

            state.Buckets[entry.Key] = entry.Value;
            state.EarliestTimestamp = state.EarliestTimestamp is null || entry.Key < state.EarliestTimestamp
                ? entry.Key
                : state.EarliestTimestamp;
            state.LatestTimestamp = state.LatestTimestamp is null || entry.Key > state.LatestTimestamp
                ? entry.Key
                : state.LatestTimestamp;
        }
    }

    private static List<HistoricalUsagePoint> ToHistoricalUsagePoints(
        SortedDictionary<DateTime, AggregatedBucketTotals> buckets)
    {
        return buckets
            .Select(kvp => new HistoricalUsagePoint(
                kvp.Key,
                kvp.Value.Granted,
                kvp.Value.Denied,
                kvp.Value.Released,
                kvp.Value.Active))
            .ToList();
    }

    private static IEnumerable<KeyValuePair<DateTime, AggregatedBucketTotals>> FilterContinuityWindow(
        SortedDictionary<DateTime, AggregatedBucketTotals> aggregated,
        BucketGranularity requested,
        BucketGranularity candidate,
        DateTime? earliestTimestamp,
        DateTime? latestTimestamp)
    {
        if (earliestTimestamp is null || latestTimestamp is null || candidate == requested)
        {
            return aggregated;
        }

        var requestedRank = GetGranularityRank(requested);
        var candidateRank = GetGranularityRank(candidate);

        if (candidateRank < requestedRank)
        {
            return aggregated.Where(entry => entry.Key > latestTimestamp.Value);
        }

        if (candidateRank > requestedRank)
        {
            return aggregated.Where(entry => entry.Key < earliestTimestamp.Value);
        }

        return aggregated;
    }

    private static int GetGranularityRank(BucketGranularity granularity)
    {
        return granularity switch
        {
            BucketGranularity.Second => 0,
            BucketGranularity.FiveMinute => 1,
            BucketGranularity.Hour => 2,
            BucketGranularity.Day => 3,
            _ => int.MaxValue
        };
    }
}