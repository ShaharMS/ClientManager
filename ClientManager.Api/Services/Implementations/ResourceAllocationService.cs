using ClientManager.Api.Services.InternalClients.Interfaces;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Proxies resource slot acquisition and release to the storage-facing runtime API.
/// Keeps the public allocation endpoints stable while moving counter ownership,
/// cleanup, and rate-limit state behind the internal service boundary.
/// </summary>
public class ResourceAllocationService : IResourceAllocationService
{
    private readonly IRuntimeStateClient _runtimeStateClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourceAllocationService"/>.
    /// </summary>
    /// <param name="runtimeStateClient">Typed client for the storage-facing runtime endpoints.</param>
    public ResourceAllocationService(IRuntimeStateClient runtimeStateClient)
    {
        _runtimeStateClient = runtimeStateClient;
    }

    /// <inheritdoc />
    public Task<ResourceAcquireResponse> AcquireAsync(
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken = default) =>
        _runtimeStateClient.AcquireAsync(new AcquireResourceRequest(clientId, resourcePoolId), cancellationToken);

    /// <inheritdoc />
    public Task<ResourceReleaseResponse> ReleaseAsync(string allocationId, CancellationToken cancellationToken = default) =>
        _runtimeStateClient.ReleaseAsync(new ReleaseResourceRequest(allocationId), cancellationToken);
}
