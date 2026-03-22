using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Interfaces;

/// <summary>
/// Repository for managing resource allocation state, including active counts and TTL-based cleanup.
/// </summary>
public interface IResourceAllocationRepository
{
    /// <summary>
    /// Retrieves a resource allocation by its unique identifier.
    /// </summary>
    /// <param name="allocationId">The unique identifier of the allocation.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The allocation if found; otherwise <c>null</c>.</returns>
    Task<ResourceAllocation?> GetByIdAsync(string allocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active (non-released, non-expired) allocations for a resource pool.
    /// </summary>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of active allocations.</returns>
    Task<int> GetActiveCountAsync(string resourcePoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active allocations for a specific client within a resource pool.
    /// </summary>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of active allocations for the client.</returns>
    Task<int> GetActiveCountByClientAsync(string resourcePoolId, string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active (non-released, non-expired) allocations grouped by resource pool ID.
    /// Loads the collection once and groups in memory to avoid N+1 queries.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A dictionary mapping resource pool IDs to their active allocation counts.</returns>
    Task<Dictionary<string, int>> GetActiveCountsByPoolAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active allocations grouped by (resource pool ID, client ID).
    /// Loads the collection once and groups in memory to avoid N+1 queries.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A dictionary mapping (poolId, clientId) tuples to their active allocation counts.</returns>
    Task<Dictionary<(string PoolId, string ClientId), int>> GetActiveCountsByPoolAndClientAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new resource allocation.
    /// </summary>
    /// <param name="allocation">The allocation to create.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task CreateAsync(ResourceAllocation allocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an allocation as released.
    /// </summary>
    /// <param name="allocationId">The unique identifier of the allocation to release.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task MarkReleasedAsync(string allocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all expired, non-released allocations as released.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of allocations that were cleaned up.</returns>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}
