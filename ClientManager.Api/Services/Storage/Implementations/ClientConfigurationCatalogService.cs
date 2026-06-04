using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
using ClientManager.Api.Services.Storage.Interfaces;
using ClientManager.Api.Services.Storage.Utils.Extensions;
using System.Text.Json;

namespace ClientManager.Api.Services.Storage.Implementations;

/// <summary>
/// Implements client-configuration catalog behavior on top of the configuration store.
/// </summary>
public class ClientConfigurationCatalogService(
    IClientConfigurationDatabase database,
    IStorageReadCache cache) : IClientConfigurationCatalogService
{
    private const string CachePrefix = "clients";

    public Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken) =>
        cache.GetOrCreateCatalogAsync(
            $"{CachePrefix}:search:{JsonSerializer.Serialize(query)}",
            token => database.SearchAsync(query, token),
            cancellationToken);

    public Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken) =>
        cache.GetOrCreateCatalogAsync(
            $"{CachePrefix}:id:{clientId}",
            token => database.GetByIdAsync(clientId, token),
            cancellationToken);

    public Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken) =>
        InvalidateAfterAsync(database.CreateAsync(configuration, cancellationToken));

    public async Task<ClientConfiguration> UpdateAsync(
        string clientId,
        ClientConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var updated = configuration with { Id = clientId };
        await database.UpdateAsync(updated, cancellationToken);
        cache.InvalidateCatalog();
        return updated;
    }

    public Task DeleteAsync(string clientId, CancellationToken cancellationToken) =>
        InvalidateAfterAsync(database.DeleteAsync(clientId, cancellationToken));

    public async Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>?> GetServicesAsync(
        string clientId,
        PagedRequest paging,
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateCatalogAsync(
            $"clients:{clientId}:services:{paging.Page}:{paging.PageSize}",
            async token =>
            {
                var config = await database.GetByIdAsync(clientId, token);
                if (config is null)
                {
                    return null;
                }

                IReadOnlyList<KeyedEntry<ServiceAccessSettings>> entries = config.Services
                    .Select(entry => new KeyedEntry<ServiceAccessSettings>(entry.Key, entry.Value))
                    .ToList();

                return entries.ToPagedResponse(paging);
            },
            cancellationToken);
    }

    public Task<ClientLookup<ServiceAccessSettings>> GetServiceSettingsAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken) =>
        GetSubDocumentAsync(
            clientId,
            $"clients:{clientId}:service-settings:{serviceId}",
            config => new ClientLookup<ServiceAccessSettings>(true, config.Services.GetValueOrDefault(serviceId)),
            cancellationToken);

    public Task SetServiceSettingsAsync(
        string clientId,
        string serviceId,
        ServiceAccessSettings settings,
        CancellationToken cancellationToken) =>
        InvalidateAfterAsync(database.SetServiceSettingsAsync(clientId, serviceId, settings, cancellationToken));

    public Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken) =>
        InvalidateAfterAsync(database.RemoveServiceSettingsAsync(clientId, serviceId, cancellationToken));

    public async Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>?> GetResourcePoolsAsync(
        string clientId,
        PagedRequest paging,
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateCatalogAsync(
            $"clients:{clientId}:resource-pools:{paging.Page}:{paging.PageSize}",
            async token =>
            {
                var config = await database.GetByIdAsync(clientId, token);
                if (config is null)
                {
                    return null;
                }

                IReadOnlyList<KeyedEntry<ResourcePoolSettings>> entries = config.ResourcePools
                    .Select(entry => new KeyedEntry<ResourcePoolSettings>(entry.Key, entry.Value))
                    .ToList();

                return entries.ToPagedResponse(paging);
            },
            cancellationToken);
    }

    public Task<ClientLookup<ResourcePoolSettings>> GetResourcePoolSettingsAsync(
        string clientId,
        string poolId,
        CancellationToken cancellationToken) =>
        GetSubDocumentAsync(
            clientId,
            $"clients:{clientId}:resource-pool-settings:{poolId}",
            config => new ClientLookup<ResourcePoolSettings>(
                true,
                config.ResourcePools.TryGetValue(poolId, out var settings) ? settings : null),
            cancellationToken);

    public Task SetResourcePoolSettingsAsync(
        string clientId,
        string poolId,
        ResourcePoolSettings settings,
        CancellationToken cancellationToken) =>
        InvalidateAfterAsync(database.SetResourcePoolSettingsAsync(clientId, poolId, settings, cancellationToken));

    public Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken) =>
        InvalidateAfterAsync(database.RemoveResourcePoolSettingsAsync(clientId, poolId, cancellationToken));

    public Task<ClientLookup<ClientRateLimit>> GetGlobalRateLimitAsync(
        string clientId,
        CancellationToken cancellationToken) =>
        GetSubDocumentAsync(
            clientId,
            $"clients:{clientId}:global-rate-limit",
            config => new ClientLookup<ClientRateLimit>(true, config.GlobalRateLimit),
            cancellationToken);

    public async Task<bool> SetGlobalRateLimitAsync(
        string clientId,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken)
    {
        var config = await database.GetByIdAsync(clientId, cancellationToken);
        if (config is null)
        {
            return false;
        }

        await database.UpdateAsync(config with { GlobalRateLimit = rateLimit }, cancellationToken);
        cache.InvalidateCatalog();
        return true;
    }

    public async Task<bool> RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken)
    {
        var config = await database.GetByIdAsync(clientId, cancellationToken);
        if (config is null)
        {
            return false;
        }

        await database.UpdateAsync(config with { GlobalRateLimit = null }, cancellationToken);
        cache.InvalidateCatalog();
        return true;
    }

    private Task<ClientLookup<T>> GetSubDocumentAsync<T>(
        string clientId,
        string cacheKey,
        Func<ClientConfiguration, ClientLookup<T>> mapExisting,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateCatalogAsync(
            cacheKey,
            async token =>
            {
                var config = await database.GetByIdAsync(clientId, token);
                return config is null
                    ? new ClientLookup<T>(false, default)
                    : mapExisting(config);
            },
            cancellationToken);

    private async Task InvalidateAfterAsync(Task operation)
    {
        await operation;
        cache.InvalidateCatalog();
    }

}
