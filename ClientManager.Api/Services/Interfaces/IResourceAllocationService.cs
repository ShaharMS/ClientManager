using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages resource pool slot acquisition, release, and TTL-based cleanup.
/// <para>
/// Resource pools represent finite shared resources (database connections, file handles, etc.)
/// where clients must explicitly acquire a slot before using the resource and release it when done.
/// Each allocation has a TTL: if the client fails to release, the cleanup path reclaims it.
/// </para>
/// <para>
/// Acquisition enforces three constraints in order:
/// <list type="number">
///   <item>System-wide pool capacity (<see cref="ResourcePool.MaxSlots"/>).</item>
///   <item>Per-client slot cap (<see cref="ResourcePoolSettings.MaxSlots"/>).</item>
///   <item>Global aggregate rate limit for the resource pool (if configured).</item>
/// </list>
/// </para>
/// </summary>
public interface IResourceAllocationService
{
    /// <summary>
    /// Acquires a resource slot for the specified client in the given resource pool.
    /// Validates the client is enabled, has a quota entry for the pool, and has not
    /// exceeded any of the three capacity constraints.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancels the acquisition pipeline, including rate limit checks.</param>
    /// <returns>The allocation response containing the allocation ID and expiry.</returns>
    Task<ResourceAcquireResponse> AcquireAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a previously acquired resource allocation, returning the slot to the pool.
    /// Returns whether the allocation transitioned from active to released during this request.
    /// </summary>
    /// <param name="allocationId">The unique identifier of the allocation to release.</param>
    /// <param name="cancellationToken">Cancels the release operation.</param>
    /// <returns>The release result.</returns>
    Task<ResourceReleaseResponse> ReleaseAsync(string allocationId, CancellationToken cancellationToken = default);
}
