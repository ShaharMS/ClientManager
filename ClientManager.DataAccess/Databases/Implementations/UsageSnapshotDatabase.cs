using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Persists usage snapshots in <see cref="IDocumentStore"/> and pushes filtering down to the
/// store via <see cref="DocumentQuery"/>. Segment-aware methods construct deterministic segment
/// IDs to avoid full-collection scans.
/// </summary>
public class UsageSnapshotDatabase : IUsageSnapshotDatabase
{
    private readonly IDocumentStore _store;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly SemaphoreSlim _usageOverlayLock = new(1, 1);
    private IReadOnlyDictionary<string, long>? _cachedUsageOverlay;
    private long _cachedUsageOverlayTicks;
    private static readonly TimeSpan UsageOverlayDedupeWindow = TimeSpan.FromSeconds(2);
    private const string Collection = "UsageSnapshots";

    /// <summary>
    /// Initializes a new instance of <see cref="UsageSnapshotDatabase"/>.
    /// </summary>
    /// <param name="store">The document store to delegate operations to.</param>
    /// <param name="clientConfigDatabase">Used by range queries to enumerate client IDs for segment ID construction.</param>
    public UsageSnapshotDatabase(
        IDocumentStore store,
        IClientConfigurationDatabase clientConfigDatabase)
    {
        _store = store;
        _clientConfigDatabase = clientConfigDatabase;
    }

    /// <inheritdoc />
    public Task<UsageSnapshot?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _store.GetAsync<UsageSnapshot>(Collection, id, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<UsageSnapshot>> GetByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) =>
        _store.GetManyAsync<UsageSnapshot>(Collection, ids, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<UsageSnapshot>> GetByTargetAsync(
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default) =>
        ExecuteQueryAsync(BuildQuery(targetId, targetType, granularity), cancellationToken);

    /// <inheritdoc />
    public async Task<UsageSnapshot?> GetByClientAndTargetAsync(
        string clientId,
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var id = BuildId(clientId, targetType, targetId, granularity);
        return await _store.GetAsync<UsageSnapshot>(Collection, id, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpsertAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default) =>
        _store.SetAsync(Collection, snapshot.Id, snapshot, cancellationToken);

    /// <inheritdoc />
    public Task UpsertManyAsync(
        IReadOnlyCollection<UsageSnapshot> snapshots,
        CancellationToken cancellationToken = default)
    {
        if (snapshots.Count == 0)
            return Task.CompletedTask;

        var documents = snapshots.ToDictionary(snapshot => snapshot.Id, StringComparer.Ordinal);
        return _store.SetManyAsync(Collection, documents, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(Collection, id, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<UsageSnapshot>> GetAllByGranularityAsync(
        BucketGranularity granularity,
        CancellationToken cancellationToken = default) =>
        ExecuteQueryAsync(BuildQuery(granularity: granularity), cancellationToken);

    private async Task<IReadOnlyList<UsageSnapshot>> ExecuteQueryAsync(
        DocumentQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _store.SearchAsync<UsageSnapshot>(Collection, query, cancellationToken);
        return [.. result.Items];
    }

    private static DocumentQuery BuildQuery(
        string? targetId = null,
        TargetType? targetType = null,
        BucketGranularity? granularity = null)
    {
        var query = new DocumentQuery();

        if (targetId is not null)
            query.Where(nameof(UsageSnapshot.TargetId), FilterOperator.Equals, targetId);

        if (targetType is not null)
            query.Where(nameof(UsageSnapshot.TargetType), FilterOperator.Equals, targetType.Value.ToString());

        if (granularity is not null)
            query.Where(nameof(UsageSnapshot.Granularity), FilterOperator.Equals, granularity.Value.ToString());

        return query;
    }

    /// <summary>
    /// Builds the compound document ID used as the store key.
    /// </summary>
    public static string BuildId(
        string clientId,
        TargetType targetType,
        string targetId,
        BucketGranularity granularity) =>
        $"{clientId}:{targetType}:{targetId}:{granularity}";

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSnapshot>> GetByTargetAndRangeAsync(
        string targetId, TargetType targetType, BucketGranularity granularity,
        DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
        var clientIds = clients.Select(client => client.Id);

        return await GetByTargetAndRangeAsync(
            targetId,
            targetType,
            granularity,
            from,
            to,
            clientIds,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSnapshot>> GetByTargetAndRangeAsync(
        string targetId, TargetType targetType, BucketGranularity granularity,
        DateTime from, DateTime to, IEnumerable<string> clientIds,
        CancellationToken cancellationToken = default)
    {
        return await GetByTargetsAndRangeAsync(
            new[] { targetId },
            targetType,
            granularity,
            from,
            to,
            clientIds,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSnapshot>> GetByTargetsAndRangeAsync(
        IEnumerable<string> targetIds,
        TargetType targetType,
        BucketGranularity granularity,
        DateTime from,
        DateTime to,
        IEnumerable<string> clientIds,
        CancellationToken cancellationToken = default)
    {
        var selectedTargetIds = targetIds.Distinct(StringComparer.Ordinal).ToList();
        var selectedClientIds = clientIds.Distinct(StringComparer.Ordinal).ToList();
        var segmentStarts = UsageSegmentHelper.EnumerateSegmentStarts(from, to, granularity).ToList();

        if (selectedTargetIds.Count == 0 || selectedClientIds.Count == 0 || segmentStarts.Count == 0)
        {
            return [];
        }

        var ids = new List<string>(selectedTargetIds.Count * selectedClientIds.Count * segmentStarts.Count);
        foreach (var selectedTargetId in selectedTargetIds)
        {
            foreach (var selectedClientId in selectedClientIds)
            {
                foreach (var segmentStart in segmentStarts)
                {
                    ids.Add(UsageSegmentHelper.BuildSegmentId(
                        selectedClientId,
                        targetType,
                        selectedTargetId,
                        granularity,
                        segmentStart));
                }
            }
        }

        return await GetByIdsAsync(ids, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UsageSnapshot?> GetByClientTargetAndSegmentAsync(
        string clientId, string targetId, TargetType targetType,
        BucketGranularity granularity, DateTime segmentStart,
        CancellationToken cancellationToken = default)
    {
        var id = UsageSegmentHelper.BuildSegmentId(clientId, targetType, targetId, granularity, segmentStart);
        return await _store.GetAsync<UsageSnapshot>(Collection, id, cancellationToken);
    }

    /// <inheritdoc />
    public Task IncrementPendingCountersAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken = default) =>
        _store.IncrementManyCountersAsync(entries, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, long>> GetPendingCounterValuesAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default) =>
        _store.GetManyCountersAsync(keys, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> GetPendingCounterValuesByPrefixAsync(
        string keyPrefix,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(keyPrefix, "usage:", StringComparison.Ordinal))
        {
            return await _store.GetCountersByPrefixAsync(keyPrefix, cancellationToken);
        }

        var now = Environment.TickCount64;
        var cached = _cachedUsageOverlay;
        if (cached is not null && now - _cachedUsageOverlayTicks < UsageOverlayDedupeWindow.TotalMilliseconds)
        {
            return cached;
        }

        await _usageOverlayLock.WaitAsync(cancellationToken);
        try
        {
            now = Environment.TickCount64;
            cached = _cachedUsageOverlay;
            if (cached is not null && now - _cachedUsageOverlayTicks < UsageOverlayDedupeWindow.TotalMilliseconds)
            {
                return cached;
            }

            var counters = await _store.GetCountersByPrefixAsync(keyPrefix, cancellationToken);
            _cachedUsageOverlay = counters;
            _cachedUsageOverlayTicks = Environment.TickCount64;
            return counters;
        }
        finally
        {
            _usageOverlayLock.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, long>> GetPendingCountersInRangeAsync(
        string clientId,
        TargetType targetType,
        string targetId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var prefix = UsageSegmentHelper.BuildUsageCounterScanPrefix(clientId, targetType, targetId);
        var counters = await _store.GetCountersByPrefixAsync(prefix, cancellationToken);
        return FilterCountersInSecondRange(counters, from, to);
    }

    private static IReadOnlyDictionary<string, long> FilterCountersInSecondRange(
        IReadOnlyDictionary<string, long> counters,
        DateTime from,
        DateTime to)
    {
        var start = UsageSegmentHelper.RoundDownToSecond(from);
        var end = UsageSegmentHelper.RoundDownToSecond(to);
        var result = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var (key, value) in counters)
        {
            if (value <= 0 ||
                !UsageSegmentHelper.TryParseUsageCounterKey(
                    key, out _, out _, out _, out var secondTimestamp, out _, out _))
            {
                continue;
            }

            if (secondTimestamp < start || secondTimestamp > end)
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task ResetPendingCountersAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var distinct = keys.Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 0)
        {
            return;
        }

        await _store.ResetManyCountersAsync(distinct, cancellationToken);
        _cachedUsageOverlay = null;
    }
}
