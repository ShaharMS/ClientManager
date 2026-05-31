using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Internal.Interfaces;

/// <summary>
/// Typed client for the storage-facing runtime endpoints that own live access and allocation state.
/// Wraps deny-by-default access checks and resource slot acquire/release so the public runtime
/// controllers never reach the storage API directly and consistently surface domain exceptions.
/// </summary>
public interface IRuntimeStateClient
{
    /// <summary>Performs a deny-by-default access check for a client and service.</summary>
    /// <param name="request">The client and service to evaluate access for.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The access decision and supporting detail.</returns>
    Task<AccessCheckResponse> CheckAccessAsync(CheckAccessRequest request, CancellationToken cancellationToken);

    /// <summary>Gets the aggregate accessibility state for a client across its configured services.</summary>
    /// <param name="clientId">The client identifier to inspect.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The client's accessibility snapshot.</returns>
    Task<ClientAccessibilityResponse> GetAccessibilityAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>Acquires a resource-pool slot on behalf of a client.</summary>
    /// <param name="request">The client and resource pool to acquire a slot from.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The acquired allocation, including its identifier.</returns>
    Task<ResourceAcquireResponse> AcquireAsync(AcquireResourceRequest request, CancellationToken cancellationToken);

    /// <summary>Releases a previously acquired resource-pool slot.</summary>
    /// <param name="request">The allocation to release.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The release outcome.</returns>
    Task<ResourceReleaseResponse> ReleaseAsync(ReleaseResourceRequest request, CancellationToken cancellationToken);
}
