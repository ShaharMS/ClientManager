using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Manages the persistence side of the resource-pool slot lifecycle: creation, active-count
/// queries, release, and TTL-based expiry cleanup.
///
/// <para>
///     This interface does <em>not</em> extend <see cref="Repositories.Interfaces.IEntityRepository{T}"/>
///     because allocations are append-heavy, never "updated" in the traditional sense (they
///     are only marked as released), and require specialised count queries used on every
///     acquire attempt to enforce system-wide and per-client slot caps.
/// </para>
///
/// <para><strong>Count queries</strong></para>
/// <para>
///     Two families exist: <em>single-key</em> counts (<see cref="GetActiveCountAsync"/>,
///     <see cref="GetActiveCountByClientAsync"/>) plus a paired hot-path read
///     (<see cref="GetActiveCountsAsync"/>) for acquire, and
///     <em>bulk-grouped</em> counts (<see cref="GetActiveCountsByPoolAsync"/>,
///     <see cref="GetActiveCountsByPoolAndClientAsync"/>) for dashboard/statistics
///     screens that need a full overview without N+1 queries.
/// </para>
/// </summary>
public interface IResourceAllocationDatabase
{
    /// <summary>
    /// Retrieves a resource allocation by its unique identifier.
    /// </summary>
    /// <param name="allocationId">The unique identifier of the allocation.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The allocation if found; otherwise <c>null</c>.</returns>
    Task<ResourceAllocation?> GetByIdAsync(string allocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active (non-released, non-expired) allocations for a resource pool.
    /// Reads from a maintained atomic counter instead of scanning all allocation documents,
    /// eliminating a full-collection <c>GetAllAsync</c> on every acquire attempt.
    /// </summary>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancels the count query - the acquire path will fail fast rather than block on a slow store.</param>
    /// <returns>The number of active allocations.</returns>
    Task<int> GetActiveCountAsync(string resourcePoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active allocations for a specific client within a resource pool.
    /// Reads from a maintained atomic counter instead of scanning all allocation documents,
    /// eliminating a full-collection <c>GetAllAsync</c> on every acquire attempt.
    /// </summary>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancels the count query - the acquire path will fail fast rather than block on a slow store.</param>
    /// <returns>The number of active allocations for the client.</returns>
    Task<int> GetActiveCountByClientAsync(string resourcePoolId, string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets both active allocation counters needed by the acquire hot path in one storage call.
    /// </summary>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancels the paired count query if the backing store is unresponsive.</param>
    /// <returns>The active pool-wide count and the active count for the client within that pool.</returns>
    Task<(int PoolCount, int ClientCount)> GetActiveCountsAsync(
        string resourcePoolId,
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active (non-released, non-expired) allocations grouped by resource pool ID.
    /// Loads the collection once and groups in memory to avoid N+1 queries.
    /// </summary>
    /// <param name="cancellationToken">Cancels the bulk query early if the caller is shutting down.</param>
    /// <returns>A dictionary mapping resource pool IDs to their active allocation counts.</returns>
    Task<Dictionary<string, int>> GetActiveCountsByPoolAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active allocations grouped by (resource pool ID, client ID).
    /// Loads the collection once and groups in memory to avoid N+1 queries.
    /// </summary>
    /// <param name="cancellationToken">Cancels the bulk query early if the caller is shutting down.</param>
    /// <returns>A dictionary mapping (poolId, clientId) tuples to their active allocation counts.</returns>
    Task<Dictionary<(string PoolId, string ClientId), int>> GetActiveCountsByPoolAndClientAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new resource allocation.
    /// </summary>
    /// <param name="allocation">The allocation to create.</param>
    /// <param name="cancellationToken">Cancels the write before the allocation is persisted.</param>
    Task CreateAsync(ResourceAllocation allocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically reserves allocation counters before the allocation document is written.
    /// </summary>
    Task<SlotReservationResult> TryReserveSlotAsync(
        string resourcePoolId,
        string clientId,
        int poolMaxSlots,
        int clientMaxSlots,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases counters reserved by <see cref="TryReserveSlotAsync"/> when document creation fails.
    /// </summary>
    Task ReleaseReservedSlotAsync(
        string resourcePoolId,
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an allocation as released.
    /// </summary>
    /// <param name="allocationId">The unique identifier of the allocation to release.</param>
    /// <param name="cancellationToken">Cancels the update before the release is persisted.</param>
    Task MarkReleasedAsync(string allocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a previously loaded allocation as released without rereading it from storage.
    /// </summary>
    /// <param name="allocation">The allocation state already loaded by the caller.</param>
    /// <param name="cancellationToken">Cancels the update before the release is persisted.</param>
    Task MarkReleasedAsync(ResourceAllocation allocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all expired, non-released allocations as released.
    /// </summary>
    /// <param name="cancellationToken">Cancels the cleanup scan - any allocations already marked in this batch remain marked, but the scan stops early.</param>
    /// <returns>The number of allocations that were cleaned up.</returns>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a full collection scan and resets the maintained atomic counters to match
    /// the actual active allocation counts. Called on the first cleanup cycle to correct
    /// any drift between counters and ground truth.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reconciliation scan.</param>
    Task ReconcileCountersAsync(CancellationToken cancellationToken = default);
}
