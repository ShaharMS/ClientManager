using ClientManager.Api.Models.Responses;

namespace ClientManager.Api.Interfaces;

/// <summary>
/// Evaluates deny-by-default access policies for clients against services.
/// </summary>
public interface IAccessControlService
{
    /// <summary>
    /// Checks whether a client is permitted to access a specific service.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The access check result indicating whether access is granted and any rate limit information.</returns>
    Task<AccessCheckResponse> CheckAccessAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a full accessibility report for a client across all services.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The client accessibility report listing all services and their access status.</returns>
    Task<ClientAccessibilityResponse> GetClientAccessibilityAsync(string clientId, CancellationToken cancellationToken = default);
}
