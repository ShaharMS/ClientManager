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
    private const string Collection = "UsageSnapshots";

    /// <summary>
    /// Initializes a new instance of <see cref="UsageSnapshotDatabase"/>.
    /// </summary>
    /// <param name="store">The document store to delegate operations to.</param>
    /// <param name="clientConfigDatabase">Used by range queries to enumerate client IDs for segment ID construction.</param>
    public UsageSnapshotDatabase(IDocumentStore store, IClientConfigurationDatabase clientConfigDatabase)
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
    public async Task<IReadOnlyList<UsageSnapshot>> GetByTargetAsync(
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var query = new DocumentQuery()
            .Where(nameof(UsageSnapshot.TargetId), FilterOperator.Equals, targetId)
            .Where(nameof(UsageSnapshot.TargetType), FilterOperator.Equals, targetType.ToString())
            .Where(nameof(UsageSnapshot.Granularity), FilterOperator.Equals, granularity.ToString());

        var result = await _store.SearchAsync<UsageSnapshot>(Collection, query, cancellationToken);
        return [.. result.Items];
    }

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
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(Collection, id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSnapshot>> GetAllByGranularityAsync(
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var query = new DocumentQuery()
            .Where(nameof(UsageSnapshot.Granularity), FilterOperator.Equals, granularity.ToString());

        var result = await _store.SearchAsync<UsageSnapshot>(Collection, query, cancellationToken);
        return [.. result.Items];
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
}
