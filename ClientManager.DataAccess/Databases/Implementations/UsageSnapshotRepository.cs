using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Persists usage snapshots in <see cref="IDocumentStore"/> and performs in-memory filtering for queries.
/// </summary>
public class UsageSnapshotRepository : IUsageSnapshotRepository
{
    private readonly IDocumentStore _store;
    private const string Collection = "UsageSnapshots";

    /// <summary>
    /// Initializes a new instance of <see cref="UsageSnapshotRepository"/>.
    /// </summary>
    /// <param name="store">The document store to delegate operations to.</param>
    public UsageSnapshotRepository(IDocumentStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<UsageSnapshot?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _store.GetAsync<UsageSnapshot>(Collection, id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSnapshot>> GetByTargetAsync(
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<UsageSnapshot>(Collection, cancellationToken);
        return all.Where(s =>
            s.TargetId == targetId &&
            s.TargetType == targetType &&
            s.Granularity == granularity).ToList();
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
    public async Task<IReadOnlyList<UsageSnapshot>> GetAllByGranularityAsync(
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<UsageSnapshot>(Collection, cancellationToken);
        return all.Where(s => s.Granularity == granularity).ToList();
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
}
