using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Persistence layer for <see cref="UsageSnapshot"/> documents - the time-series records
/// that store aggregated <see cref="UsageEventType"/> counts.
///
/// <para>
///     The background <c>UsagePersistenceService</c> periodically drains the in-memory
///     usage buffer and calls <see cref="UpsertAsync"/> to merge new counts into existing
///     snapshots (or create them on first flush). Read methods serve the admin dashboard
///     and statistics APIs.
/// </para>
///
/// <para><strong>Query patterns</strong></para>
/// <list type="bullet">
///     <item>
///         <description>
///             <see cref="GetByClientAndTargetAsync"/> - single client + target at one
///             granularity, used during each buffer flush to find the document to merge into.
///         </description>
///     </item>
///     <item>
///         <description>
///             <see cref="GetByTargetAsync"/> - all clients for a given target, used by
///             per-target dashboards (e.g. "show me who's using service X").
///         </description>
///     </item>
///     <item>
///         <description>
///             <see cref="GetAllByGranularityAsync"/> - all snapshots at one granularity,
///             used for system-wide rollup and retention cleanup.
///         </description>
///     </item>
/// </list>
/// </summary>
public interface IUsageSnapshotRepository
{
    /// <summary>
    /// Gets a usage snapshot by its compound key.
    /// </summary>
    /// <param name="id">The compound key identifying the snapshot.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The snapshot if found; otherwise <c>null</c>.</returns>
    Task<UsageSnapshot?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all snapshots for a specific target at a specific granularity.
    /// </summary>
    /// <param name="targetId">The target identifier (service or resource pool).</param>
    /// <param name="targetType">Whether the target is a Service or ResourcePool.</param>
    /// <param name="granularity">The bucket granularity to filter by.</param>
    /// <param name="cancellationToken">Cancels the query early if the caller is shutting down.</param>
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
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
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
    /// <param name="cancellationToken">Cancels the write. If cancelled mid-upsert, the snapshot may be partially updated depending on the backend.</param>
    Task UpsertAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all snapshots at a specific granularity level.
    /// </summary>
    /// <param name="granularity">The bucket granularity to filter by.</param>
    /// <param name="cancellationToken">Cancels the enumeration early if the caller is shutting down.</param>
    /// <returns>All snapshots matching the given granularity.</returns>
    Task<IReadOnlyList<UsageSnapshot>> GetAllByGranularityAsync(
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);
}
