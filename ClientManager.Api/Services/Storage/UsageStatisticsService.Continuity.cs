using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Stitches rolled-up and live usage buckets into one continuous history.
/// </summary>
public partial class UsageStatisticsService
{
    private readonly record struct AggregatedBucketTotals(
        long Granted,
        long DeniedUnauthenticated,
        long DeniedBlocked,
        long DeniedRateLimited,
        long DeniedCapacityLimited,
        long Released,
        long Active)
    {
        public long Denied => DeniedUnauthenticated + DeniedBlocked + DeniedRateLimited + DeniedCapacityLimited;

        public AggregatedBucketTotals AddDenied(UsageDenialCategory? category, long value) => category switch
        {
            UsageDenialCategory.Unauthenticated => this with { DeniedUnauthenticated = DeniedUnauthenticated + value },
            UsageDenialCategory.Blocked => this with { DeniedBlocked = DeniedBlocked + value },
            UsageDenialCategory.RateLimited => this with { DeniedRateLimited = DeniedRateLimited + value },
            UsageDenialCategory.CapacityLimited => this with { DeniedCapacityLimited = DeniedCapacityLimited + value },
            _ => this with { DeniedBlocked = DeniedBlocked + value }
        };
    }

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

        await OverlayUsageCountersByTargetAsync(states, targetType, selectedClientIds, from, to, cancellationToken);

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

        await OverlayUsageCountersByTargetClientAsync(states, targetType, selectedClientIds, from, to, cancellationToken);

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
                var denied = bucket.GetDeniedBreakdown();
                aggregated[bucket.Timestamp] = new AggregatedBucketTotals(
                    existing.Granted + bucket.GrantedCount,
                    existing.DeniedUnauthenticated + denied.Unauth,
                    existing.DeniedBlocked + denied.Blocked,
                    existing.DeniedRateLimited + denied.RateLimited,
                    existing.DeniedCapacityLimited + denied.Capacity,
                    existing.Released + bucket.ReleasedCount,
                    existing.Active + bucket.ActiveCount);
                continue;
            }

            var breakdown = bucket.GetDeniedBreakdown();
            aggregated[bucket.Timestamp] = new AggregatedBucketTotals(
                bucket.GrantedCount,
                breakdown.Unauth,
                breakdown.Blocked,
                breakdown.RateLimited,
                breakdown.Capacity,
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

        foreach (var entry in FilterContinuityWindow(
            aggregated, requested, candidate, state.ActualGranularity, state.EarliestTimestamp, state.LatestTimestamp))
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
        var runningActive = 0L;
        return buckets
            .OrderBy(kvp => kvp.Key)
            .Select(kvp =>
            {
                runningActive = Math.Max(0, runningActive + kvp.Value.Granted - kvp.Value.Released);
                var active = kvp.Value.Active > 0 ? kvp.Value.Active : runningActive;
                return new HistoricalUsagePoint(
                    kvp.Key,
                    kvp.Value.Granted,
                    kvp.Value.Denied,
                    kvp.Value.DeniedUnauthenticated,
                    kvp.Value.DeniedBlocked,
                    kvp.Value.DeniedRateLimited,
                    kvp.Value.DeniedCapacityLimited,
                    kvp.Value.Released,
                    active);
            })
            .ToList();
    }

    private static long ComputeRunningActive(
        SortedDictionary<DateTime, AggregatedBucketTotals> buckets,
        DateTime upTo)
    {
        var runningActive = 0L;
        foreach (var (timestamp, totals) in buckets.OrderBy(kvp => kvp.Key))
        {
            if (timestamp > upTo)
            {
                break;
            }

            runningActive = Math.Max(0, runningActive + totals.Granted - totals.Released);
            if (totals.Active > 0)
            {
                runningActive = totals.Active;
            }
        }

        return runningActive;
    }

    private static IEnumerable<KeyValuePair<DateTime, AggregatedBucketTotals>> FilterContinuityWindow(
        SortedDictionary<DateTime, AggregatedBucketTotals> aggregated,
        BucketGranularity requested,
        BucketGranularity candidate,
        BucketGranularity establishedGranularity,
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
            var exclusiveAfter = latestTimestamp.Value + GetBucketDuration(establishedGranularity);
            return aggregated.Where(entry => entry.Key >= exclusiveAfter);
        }

        if (candidateRank > requestedRank)
        {
            return aggregated.Where(entry => entry.Key + GetBucketDuration(candidate) <= earliestTimestamp.Value);
        }

        return aggregated;
    }

    private static TimeSpan GetBucketDuration(BucketGranularity granularity) => granularity switch
    {
        BucketGranularity.Second => TimeSpan.FromSeconds(1),
        BucketGranularity.FiveMinute => TimeSpan.FromMinutes(5),
        BucketGranularity.Hour => TimeSpan.FromHours(1),
        BucketGranularity.Day => TimeSpan.FromDays(1),
        _ => TimeSpan.FromMinutes(5)
    };

    private static long SumGrantedInRange(
        SortedDictionary<DateTime, AggregatedBucketTotals> buckets,
        DateTime from,
        DateTime to) =>
        buckets.Where(kvp => kvp.Key >= from && kvp.Key <= to).Sum(kvp => kvp.Value.Granted);

    private static long SumDeniedInRange(
        SortedDictionary<DateTime, AggregatedBucketTotals> buckets,
        DateTime from,
        DateTime to) =>
        buckets.Where(kvp => kvp.Key >= from && kvp.Key <= to).Sum(kvp => kvp.Value.Denied);

    private static (long Unauth, long Blocked, long RateLimited, long Capacity) SumDeniedBreakdownInRange(
        SortedDictionary<DateTime, AggregatedBucketTotals> buckets,
        DateTime from,
        DateTime to)
    {
        long unauth = 0, blocked = 0, rateLimited = 0, capacity = 0;
        foreach (var kvp in buckets.Where(kvp => kvp.Key >= from && kvp.Key <= to))
        {
            unauth += kvp.Value.DeniedUnauthenticated;
            blocked += kvp.Value.DeniedBlocked;
            rateLimited += kvp.Value.DeniedRateLimited;
            capacity += kvp.Value.DeniedCapacityLimited;
        }

        return (unauth, blocked, rateLimited, capacity);
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

    private async Task OverlayUsageCountersByTargetAsync(
        IReadOnlyDictionary<string, ContinuousBucketState> states,
        TargetType targetType,
        IReadOnlyList<string> clientIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var overlayFrom = MaxDateTime(from, DateTime.UtcNow - _usageTrackingOptions.SecondRetention);
        if (overlayFrom > to)
        {
            return;
        }

        foreach (var targetId in states.Keys)
        {
            foreach (var clientId in clientIds)
            {
                var counterValues = await _usageSnapshotDatabase.GetPendingCountersInRangeAsync(
                    clientId, targetType, targetId, overlayFrom, to, cancellationToken);
                ApplyOverlayCounterValues(counterValues, states[targetId], from, to);
            }
        }
    }

    private async Task OverlayUsageCountersByTargetClientAsync(
        IReadOnlyDictionary<(string TargetId, string ClientId), ContinuousBucketState> states,
        TargetType targetType,
        IReadOnlyList<string> clientIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var overlayFrom = MaxDateTime(from, DateTime.UtcNow - _usageTrackingOptions.SecondRetention);
        if (overlayFrom > to)
        {
            return;
        }

        foreach (var (targetId, clientId) in states.Keys)
        {
            var counterValues = await _usageSnapshotDatabase.GetPendingCountersInRangeAsync(
                clientId, targetType, targetId, overlayFrom, to, cancellationToken);
            if (!states.TryGetValue((targetId, clientId), out var state))
            {
                continue;
            }

            ApplyOverlayCounterValues(counterValues, state, from, to);
        }
    }

    private void ApplyOverlayCounterValues(
        IReadOnlyDictionary<string, long> counterValues,
        ContinuousBucketState state,
        DateTime from,
        DateTime to)
    {
        foreach (var (storageKey, value) in counterValues)
        {
            if (value <= 0 ||
                !UsageSegmentHelper.TryParseUsageCounterKey(
                    storageKey, out _, out _, out _, out var secondTimestamp, out var eventType, out var denialCategory))
            {
                continue;
            }

            var bucketTimestamp = MapCounterTimestamp(secondTimestamp, state.ActualGranularity);
            if (bucketTimestamp < from || bucketTimestamp > to)
            {
                continue;
            }

            if (!state.Buckets.TryGetValue(bucketTimestamp, out var totals))
            {
                totals = new AggregatedBucketTotals(0, 0, 0, 0, 0, 0, 0);
            }

            state.Buckets[bucketTimestamp] = eventType switch
            {
                UsageEventType.Granted => totals with
                {
                    Granted = totals.Granted + value,
                    Active = totals.Active + value
                },
                UsageEventType.Denied => totals.AddDenied(denialCategory, value),
                UsageEventType.Released => totals with
                {
                    Released = totals.Released + value,
                    Active = Math.Max(0, totals.Active - value)
                },
                _ => totals
            };
            state.FoundAny = true;
        }
    }

    private static DateTime MapCounterTimestamp(DateTime secondTimestamp, BucketGranularity granularity)
    {
        return granularity switch
        {
            BucketGranularity.Second => UsageSegmentHelper.RoundDownToSecond(secondTimestamp),
            BucketGranularity.FiveMinute => RoundDownToFiveMinutes(secondTimestamp),
            BucketGranularity.Hour => new DateTime(secondTimestamp.Year, secondTimestamp.Month, secondTimestamp.Day, secondTimestamp.Hour, 0, 0, DateTimeKind.Utc),
            BucketGranularity.Day => new DateTime(secondTimestamp.Year, secondTimestamp.Month, secondTimestamp.Day, 0, 0, 0, DateTimeKind.Utc),
            _ => RoundDownToFiveMinutes(secondTimestamp)
        };
    }

    private static DateTime MaxDateTime(DateTime left, DateTime right) => left > right ? left : right;

    private static DateTime RoundDownToFiveMinutes(DateTime utc) =>
        new(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 5 * 5, 0, DateTimeKind.Utc);
}