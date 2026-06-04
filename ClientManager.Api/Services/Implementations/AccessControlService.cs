using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Responses;
using StorageAccessControlService = ClientManager.Api.Services.Storage.Interfaces.IAccessControlService;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts deny-by-default access checks onto the in-process storage access-control service.
/// Keeps the public controller surface unchanged while the rate-limit state, usage recording,
/// and configuration-backed decision logic run in-process.
/// </summary>
public class AccessControlService : IAccessControlService
{
    private readonly StorageAccessControlService _accessControlService;

    /// <summary>
    /// Initializes a new instance of <see cref="AccessControlService"/>.
    /// </summary>
    /// <param name="accessControlService">In-process storage access-control service.</param>
    public AccessControlService(StorageAccessControlService accessControlService)
    {
        _accessControlService = accessControlService;
    }

    /// <inheritdoc />
    public Task<AccessCheckResponse> CheckAccessAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default) =>
        _accessControlService.CheckAccessAsync(clientId, serviceId, cancellationToken);

    /// <inheritdoc />
    public Task<ClientAccessibilityResponse> GetClientAccessibilityAsync(
        string clientId,
        CancellationToken cancellationToken = default) =>
        _accessControlService.GetClientAccessibilityAsync(clientId, cancellationToken);
}
