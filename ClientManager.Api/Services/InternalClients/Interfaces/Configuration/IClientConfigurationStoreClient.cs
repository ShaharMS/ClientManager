using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Interfaces.Configuration;

// CR: Interface needs documentation. Class should have doc explaining purpose and why it exists somewhat briefly. each method should also have the same documentation - what it does, why it exists, and any important details about behavior/context, with explanitory parameter descriptions.
public interface IClientConfigurationStoreClient
{
    Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken);

    Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken);

    Task UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken);

    Task DeleteAsync(string clientId, CancellationToken cancellationToken);

    Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>> GetServicesAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken);

    Task<ServiceAccessSettings?> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken);

    Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken);

    Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken);

    Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>> GetResourcePoolsAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken);

    Task<ResourcePoolSettings?> GetResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken);

    Task SetResourcePoolSettingsAsync(string clientId, string poolId, ResourcePoolSettings settings, CancellationToken cancellationToken);

    Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken);

    Task<ClientRateLimit?> GetGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken);

    Task SetGlobalRateLimitAsync(string clientId, ClientRateLimit rateLimit, CancellationToken cancellationToken);

    Task RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken);
}