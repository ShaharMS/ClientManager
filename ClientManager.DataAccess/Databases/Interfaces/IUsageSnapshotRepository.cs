using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Repository for persisting and querying time-bucketed usage snapshots.
/// </summary>
public interface IUsageSnapshotRepository
{
    /// <summary>
    /// Gets a usage snapshot by its compound key.
    /// </summary>
    /// <param name="id">The compound key identifying the snapshot.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The snapshot if found; otherwise <c>null</c>.</returns>
    Task<UsageSnapshot?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all snapshots for a specific target at a specific granularity.
    /// </summary>
    /// <param name="targetId">The target identifier (service or resource pool).</param>
    /// <param name="targetType">Whether the target is a Service or ResourcePool.</param>
    /// <param name="granularity">The bucket granularity to filter by.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>All matching snapshots.</returns>
    Task<IReadOnlyList<UsageSnapshot>> GetByTargetAsync(
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a snapshot for a specific client-target-granularity combination.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="targetId">The target identifier (service or resource pool).</param>
    /// <param name="targetType">Whether the target is a Service or ResourcePool.</param>
    /// <param name="granularity">The bucket granularity.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The snapshot if found; otherwise <c>null</c>.</returns>
    Task<UsageSnapshot?> GetByClientAndTargetAsync(
        string clientId,
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a usage snapshot document.
    /// </summary>
    /// <param name="snapshot">The snapshot to persist.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task UpsertAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all snapshots at a specific granularity level.
    /// </summary>
    /// <param name="granularity">The bucket granularity to filter by.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>All snapshots matching the given granularity.</returns>
    Task<IReadOnlyList<UsageSnapshot>> GetAllByGranularityAsync(
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);
}
