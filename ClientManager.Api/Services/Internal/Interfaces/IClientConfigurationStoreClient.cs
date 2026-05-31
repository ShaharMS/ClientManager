using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Internal.Interfaces;

/// <summary>
/// Typed client for the storage-facing client-configuration store.
/// Exposes the per-client configuration document along with its nested service, resource-pool,
/// and global rate-limit settings so public controllers never talk to the storage API directly.
/// </summary>
public interface IClientConfigurationStoreClient
{
    /// <summary>Searches client configuration documents matching the supplied query.</summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching client configurations and total hit count.</returns>
    Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Retrieves a single client configuration by its identifier.</summary>
    /// <param name="clientId">The client identifier to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching client configuration.</returns>
    Task<ClientConfiguration> GetByIdAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>Creates a new client configuration document.</summary>
    /// <param name="configuration">The configuration to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken);

    /// <summary>Replaces an existing client configuration document.</summary>
    /// <param name="clientId">The client identifier being updated.</param>
    /// <param name="configuration">The replacement configuration.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken);

    /// <summary>Deletes a client configuration document.</summary>
    /// <param name="clientId">The client identifier to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task DeleteAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>Lists the service access settings configured for a client.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="paging">The requested page and page size.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>A page of keyed service access settings.</returns>
    Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>> GetServicesAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken);

    /// <summary>Gets the access settings for a specific service under a client.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="serviceId">The service identifier to read.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The service access settings, or <see langword="null"/> when none are configured.</returns>
    Task<ServiceAccessSettings?> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken);

    /// <summary>Creates or replaces the access settings for a service under a client.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="serviceId">The service identifier being configured.</param>
    /// <param name="settings">The access settings to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken);

    /// <summary>Removes the access settings for a service under a client.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="serviceId">The service identifier to clear.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken);

    /// <summary>Lists the resource-pool settings configured for a client.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="paging">The requested page and page size.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>A page of keyed resource-pool settings.</returns>
    Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>> GetResourcePoolsAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken);

    /// <summary>Gets the settings for a specific resource pool under a client.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="poolId">The resource-pool identifier to read.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The resource-pool settings, or <see langword="null"/> when none are configured.</returns>
    Task<ResourcePoolSettings?> GetResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken);

    /// <summary>Creates or replaces the settings for a resource pool under a client.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="poolId">The resource-pool identifier being configured.</param>
    /// <param name="settings">The settings to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task SetResourcePoolSettingsAsync(string clientId, string poolId, ResourcePoolSettings settings, CancellationToken cancellationToken);

    /// <summary>Removes the settings for a resource pool under a client.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="poolId">The resource-pool identifier to clear.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken);

    /// <summary>Gets the client-wide global rate limit, if one is configured.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The configured client rate limit, or <see langword="null"/> when none exists.</returns>
    Task<ClientRateLimit?> GetGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>Creates or replaces the client-wide global rate limit.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="rateLimit">The rate limit to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task SetGlobalRateLimitAsync(string clientId, ClientRateLimit rateLimit, CancellationToken cancellationToken);

    /// <summary>Removes the client-wide global rate limit.</summary>
    /// <param name="clientId">The owning client identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken);
}
