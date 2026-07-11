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
/// <para><strong>Segmented storage</strong></para>
/// <para>
///     Snapshots are stored as bounded time-segment documents rather than a single
///     ever-growing document per (client, target, granularity). Each segment covers a
///     fixed window (e.g. 1 hour for second-granularity data) and is identified by a
///     compound key that includes the segment start time. This keeps individual documents
///     small regardless of retention length. See <c>UsageSegmentHelper</c> for segment
///     window sizes and ID construction.
/// </para>
///
/// <para><strong>Query patterns</strong></para>
/// <list type="bullet">
///     <item>
///         <description>
///             <see cref="GetByClientTargetAndSegmentAsync"/> — direct lookup of a single
///             segment document by constructing its ID. Fastest path, used by flush.
///         </description>
///     </item>
///     <item>
///         <description>
///             <see cref="GetByTargetAndRangeAsync"/> — fetches all client segments
///             overlapping a time range by enumerating segment IDs. Avoids scanning the
///             entire collection.
///         </description>
///     </item>
///     <item>
///         <description>
///             <see cref="GetByTargetAsync"/> — all clients for a given target (legacy
///             full-scan method, used where a time range is not available).
///         </description>
///     </item>
///     <item>
///         <description>
///             <see cref="GetAllByGranularityAsync"/> — all snapshots at one granularity.
///             Used by the background rollup and prune cycles where scanning is acceptable.
///         </description>
///     </item>
/// </list>
/// </summary>
public interface IUsageSnapshotDatabase
{
    /// <summary>
    /// Gets a usage snapshot by its compound key.
    /// </summary>
    /// <param name="id">The compound key identifying the snapshot.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The snapshot if found; otherwise <c>null</c>.</returns>
    Task<UsageSnapshot?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage snapshots by their compound keys.
    /// </summary>
    /// <param name="ids">The compound keys identifying the snapshots.</param>
    /// <param name="cancellationToken">Cancels the batch lookup if the store is unresponsive.</param>
    /// <returns>Only the snapshots that were found; missing IDs are omitted.</returns>
    Task<IReadOnlyList<UsageSnapshot>> GetByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default);

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
    /// Creates or updates multiple usage snapshot documents in one storage batch.
    /// </summary>
    /// <param name="snapshots">The snapshots to persist.</param>
    /// <param name="cancellationToken">Cancels the batch write. Backends may keep snapshots already written before cancellation.</param>
    Task UpsertManyAsync(
        IReadOnlyCollection<UsageSnapshot> snapshots,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a usage snapshot document by its compound key. Used by prune and rollup to
    /// drop whole segment documents instead of rewriting them with an empty bucket list.
    /// </summary>
    /// <param name="id">The compound key identifying the snapshot to delete.</param>
    /// <param name="cancellationToken">Cancels the delete if the store is unresponsive.</param>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all snapshots at a specific granularity level.
    /// </summary>
    /// <param name="granularity">The bucket granularity to filter by.</param>
    /// <param name="cancellationToken">Cancels the enumeration early if the caller is shutting down.</param>
    /// <returns>All snapshots matching the given granularity.</returns>
    Task<IReadOnlyList<UsageSnapshot>> GetAllByGranularityAsync(
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all snapshots for a target within a time range by constructing segment IDs for
    /// each known client instead of scanning the entire collection. The number of store reads
    /// is bounded by (client count × segment count) rather than the total document count.
    /// </summary>
    /// <param name="targetId">The target identifier (service or resource pool).</param>
    /// <param name="targetType">Whether the target is a Service or ResourcePool.</param>
    /// <param name="granularity">The bucket granularity to filter by.</param>
    /// <param name="from">Inclusive lower bound of the time range.</param>
    /// <param name="to">Exclusive upper bound of the time range.</param>
    /// <param name="cancellationToken">Cancels the query early if the caller is shutting down.</param>
    /// <returns>All matching snapshots that contain at least one bucket in the requested range.</returns>
    Task<IReadOnlyList<UsageSnapshot>> GetByTargetAndRangeAsync(
        string targetId, TargetType targetType, BucketGranularity granularity,
        DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshots for a target within a time range, constrained to a known set of client IDs.
    /// </summary>
    /// <param name="targetId">The target identifier (service or resource pool).</param>
    /// <param name="targetType">Whether the target is a Service or ResourcePool.</param>
    /// <param name="granularity">The bucket granularity to filter by.</param>
    /// <param name="from">Inclusive lower bound of the time range.</param>
    /// <param name="to">Exclusive upper bound of the time range.</param>
    /// <param name="clientIds">The client identifiers to include in the range query.</param>
    /// <param name="cancellationToken">Cancels the query early if the caller is shutting down.</param>
    /// <returns>All matching snapshots for the requested clients and segment range.</returns>
    Task<IReadOnlyList<UsageSnapshot>> GetByTargetAndRangeAsync(
        string targetId, TargetType targetType, BucketGranularity granularity,
        DateTime from, DateTime to, IEnumerable<string> clientIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshots for multiple targets within a time range, constrained to a known set of client IDs.
    /// </summary>
    /// <param name="targetIds">The target identifiers (services or resource pools) to include.</param>
    /// <param name="targetType">Whether the targets are Services or ResourcePools.</param>
    /// <param name="granularity">The bucket granularity to filter by.</param>
    /// <param name="from">Inclusive lower bound of the time range.</param>
    /// <param name="to">Exclusive upper bound of the time range.</param>
    /// <param name="clientIds">The client identifiers to include in the range query.</param>
    /// <param name="cancellationToken">Cancels the query early if the caller is shutting down.</param>
    /// <returns>All matching snapshots for the requested targets, clients, and segment range.</returns>
    Task<IReadOnlyList<UsageSnapshot>> GetByTargetsAndRangeAsync(
        IEnumerable<string> targetIds,
        TargetType targetType,
        BucketGranularity granularity,
        DateTime from,
        DateTime to,
        IEnumerable<string> clientIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single snapshot for a specific client-target-granularity-segment combination
    /// by constructing the segment ID directly — a single <c>GetByIdAsync</c> call with no scanning.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="targetId">The target identifier (service or resource pool).</param>
    /// <param name="targetType">Whether the target is a Service or ResourcePool.</param>
    /// <param name="granularity">The bucket granularity.</param>
    /// <param name="segmentStart">The start of the segment window.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The snapshot if found; otherwise <c>null</c>.</returns>
    Task<UsageSnapshot?> GetByClientTargetAndSegmentAsync(
        string clientId, string targetId, TargetType targetType,
        BucketGranularity granularity, DateTime segmentStart,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments pending usage counters before rollup folds them into snapshots.
    /// </summary>
    Task IncrementPendingCountersAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads pending usage counter values for overlay and rollup.
    /// </summary>
    Task<IReadOnlyDictionary<string, long>> GetPendingCounterValuesAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads non-zero pending usage counters whose keys start with <paramref name="keyPrefix"/>.
    /// </summary>
    Task<IReadOnlyDictionary<string, long>> GetPendingCounterValuesByPrefixAsync(
        string keyPrefix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads pending usage counters for a client/target in a second-level time window.
    /// </summary>
    Task<IReadOnlyDictionary<string, long>> GetPendingCountersInRangeAsync(
        string clientId,
        TargetType targetType,
        string targetId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears pending usage counters after their values were folded into snapshots.
    /// </summary>
    Task ResetPendingCountersAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts all usage snapshot documents in the store.
    /// </summary>
    Task<long> CountAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of snapshots ordered by document id (for seed export/delete pagination).
    /// </summary>
    Task<IReadOnlyList<UsageSnapshot>> GetPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
