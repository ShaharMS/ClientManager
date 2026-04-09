using ClientManager.Api.Services.InternalClients.Interfaces;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Proxies deny-by-default access checks to the storage-facing runtime API.
/// Keeps the public controller surface unchanged while moving rate-limit state,
/// usage recording, and configuration-backed decision logic behind the internal boundary.
/// </summary>
public class AccessControlService : IAccessControlService
{
    private readonly IRuntimeStateClient _runtimeStateClient;

    /// <summary>
    /// Initializes a new instance of <see cref="AccessControlService"/>.
    /// </summary>
    /// <param name="runtimeStateClient">Typed client for the storage-facing runtime endpoints.</param>
    public AccessControlService(IRuntimeStateClient runtimeStateClient)
    {
        _runtimeStateClient = runtimeStateClient;
    }

    /// <inheritdoc />
    public Task<AccessCheckResponse> CheckAccessAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default) =>
        _runtimeStateClient.CheckAccessAsync(new CheckAccessRequest(clientId, serviceId), cancellationToken);

    /// <inheritdoc />
    public Task<ClientAccessibilityResponse> GetClientAccessibilityAsync(
        string clientId,
        CancellationToken cancellationToken = default) =>
        _runtimeStateClient.GetAccessibilityAsync(clientId, cancellationToken);
}
