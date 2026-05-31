using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the per-service access settings nested under a client configuration.
/// Resolving a missing client or missing settings surfaces a typed not-found exception so the
/// controller never has to inspect null results.
/// </summary>
public interface IClientServiceSettingsService
{
    /// <summary>
    /// Lists the service access settings configured for a client, paginated.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="paging">The requested page and page size.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>A page of keyed service access settings.</returns>
    Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>> GetServicesAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the access settings for a specific service under a client.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The service access settings.</returns>
    Task<ServiceAccessSettings> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the access settings for a service under a client.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="serviceId">The unique identifier of the service being configured.</param>
    /// <param name="settings">The access settings to apply.</param>
    /// <param name="cancellationToken">Cancels the update operation.</param>
    /// <returns>The applied service access settings.</returns>
    Task<ServiceAccessSettings> SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the access settings for a service under a client, revoking access.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="serviceId">The unique identifier of the service to clear.</param>
    /// <param name="cancellationToken">Cancels the remove operation.</param>
    Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
}
