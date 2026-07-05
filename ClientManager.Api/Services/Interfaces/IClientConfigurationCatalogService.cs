using System.Text.Json;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Handles client-configuration CRUD and nested configuration reads and writes.
/// </summary>
public interface IClientConfigurationCatalogService
{
    Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    Task<ClientConfiguration> GetByIdAsync(string clientId, CancellationToken cancellationToken = default);

    Task<ClientConfiguration> CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default);

    Task<ClientConfiguration> UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken = default);

    Task DeleteAsync(string clientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatchItemResult<ClientConfiguration>>> PatchAsync(
        IReadOnlyList<JsonElement> patches,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>> GetServicesAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken = default);

    Task<ServiceAccessSettings> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);

    Task<ServiceAccessSettings> SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken = default);

    Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);

    Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>> GetResourcePoolsAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken = default);

    Task<ResourcePoolSettings> GetResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken = default);

    Task<ResourcePoolSettings> SetResourcePoolSettingsAsync(string clientId, string poolId, ResourcePoolSettings settings, CancellationToken cancellationToken = default);

    Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken = default);

    Task<ClientRateLimit> GetGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken = default);

    Task<ClientRateLimit> SetGlobalRateLimitAsync(string clientId, ClientRateLimit rateLimit, CancellationToken cancellationToken = default);

    Task RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken = default);
}
