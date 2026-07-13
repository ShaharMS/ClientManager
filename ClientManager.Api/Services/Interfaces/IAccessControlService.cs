using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Evaluates deny-by-default access policies for clients against services.
/// </summary>
public interface IAccessControlService
{
    /// <summary>
    /// Checks whether a client is permitted to access a specific service.
    /// </summary>
    Task<AccessCheckResponse> CheckAccessAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
}
