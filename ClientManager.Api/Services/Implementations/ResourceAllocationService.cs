using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Responses;
using StorageResourceAllocationService = ClientManager.Api.Services.Storage.Interfaces.IResourceAllocationService;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts resource slot acquisition and release onto the in-process storage resource-allocation
/// service. Keeps the public allocation endpoints stable while counter ownership, cleanup, and
/// rate-limit state run in-process.
/// </summary>
public class ResourceAllocationService : IResourceAllocationService
{
    private readonly StorageResourceAllocationService _resourceAllocationService;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourceAllocationService"/>.
    /// </summary>
    /// <param name="resourceAllocationService">In-process storage resource-allocation service.</param>
    public ResourceAllocationService(StorageResourceAllocationService resourceAllocationService)
    {
        _resourceAllocationService = resourceAllocationService;
    }

    /// <inheritdoc />
    public Task<ResourceAcquireResponse> AcquireAsync(
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken = default) =>
        _resourceAllocationService.AcquireAsync(clientId, resourcePoolId, cancellationToken);

    /// <inheritdoc />
    public Task<ResourceReleaseResponse> ReleaseAsync(string allocationId, CancellationToken cancellationToken = default) =>
        _resourceAllocationService.ReleaseAsync(allocationId, cancellationToken);
}
