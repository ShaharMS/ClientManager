using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.StorageApi.Services.Interfaces;

/// <summary>
/// Handles client-configuration CRUD and nested configuration reads and writes.
/// </summary>
public interface IClientConfigurationCatalogService
{
    Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken);

    Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken);

    Task<ClientConfiguration> UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken);

    Task DeleteAsync(string clientId, CancellationToken cancellationToken);

    Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>?> GetServicesAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken);

    Task<ClientLookup<ServiceAccessSettings>> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken);

    Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken);

    Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken);

    Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>?> GetResourcePoolsAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken);

    Task<ClientLookup<ResourcePoolSettings>> GetResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken);

    Task SetResourcePoolSettingsAsync(string clientId, string poolId, ResourcePoolSettings settings, CancellationToken cancellationToken);

    Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken);

    Task<ClientLookup<ClientRateLimit>> GetGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken);

    Task<bool> SetGlobalRateLimitAsync(string clientId, ClientRateLimit rateLimit, CancellationToken cancellationToken);

    Task<bool> RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken);
}