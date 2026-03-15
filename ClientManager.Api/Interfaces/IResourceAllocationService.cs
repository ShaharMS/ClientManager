using ClientManager.Api.Models.Responses;

namespace ClientManager.Api.Interfaces;

/// <summary>
/// Manages resource pool slot acquisition, release, and TTL-based cleanup.
/// </summary>
public interface IResourceAllocationService
{
    /// <summary>
    /// Acquires a resource slot for the specified client in the given resource pool.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The allocation response containing the allocation ID and expiry.</returns>
    Task<ResourceAcquireResponse> AcquireAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a previously acquired resource allocation.
    /// </summary>
    /// <param name="allocationId">The unique identifier of the allocation to release.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns><c>true</c> if the allocation was found and released; otherwise <c>false</c>.</returns>
    Task<bool> ReleaseAsync(string allocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired allocations across all resource pools.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task CleanupExpiredAllocationsAsync(CancellationToken cancellationToken = default);
}
