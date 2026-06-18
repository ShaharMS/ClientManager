using System.Text.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage;
using ClientManager.Api.Utils.Extensions;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Implements client-configuration catalog behavior on top of the configuration store.
/// </summary>
public class ClientConfigurationCatalogService(
    IClientConfigurationDatabase database,
    IStorageReadCache cache,
    ICrossPodCacheInvalidator cacheInvalidator) : IClientConfigurationCatalogService
{
    private const string CachePrefix = "clients";

    public Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        cache.GetOrCreateCatalogAsync(
            $"{CachePrefix}:search:{JsonSerializer.Serialize(query)}",
            token => database.SearchAsync(query, token),
            cancellationToken);

    public async Task<ClientConfiguration> GetByIdAsync(string clientId, CancellationToken cancellationToken = default) =>
        await TryGetByIdAsync(clientId, cancellationToken) ?? throw DomainErrors.Client(clientId);

    public async Task<ClientConfiguration> CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await InvalidateAfterAsync(database.CreateAsync(configuration, cancellationToken));
        return configuration;
    }

    public async Task<ClientConfiguration> UpdateAsync(
        string clientId,
        ClientConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _ = await GetByIdAsync(clientId, cancellationToken);
        var updated = configuration with { Id = clientId };
        await database.UpdateAsync(updated, cancellationToken);
        cacheInvalidator.PublishCatalogInvalidation();
        return updated;
    }

    public async Task DeleteAsync(string clientId, CancellationToken cancellationToken = default)
    {
        _ = await GetByIdAsync(clientId, cancellationToken);
        await InvalidateAfterAsync(database.DeleteAsync(clientId, cancellationToken));
    }

    public async Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>> GetServicesAsync(
        string clientId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        IReadOnlyList<KeyedEntry<ServiceAccessSettings>> entries = config.Services
            .Select(entry => new KeyedEntry<ServiceAccessSettings>(entry.Key, entry.Value))
            .ToList();

        return entries.ToPagedResponse(paging);
    }

    public async Task<ServiceAccessSettings> GetServiceSettingsAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var lookup = await GetServiceSettingsLookupAsync(clientId, serviceId, cancellationToken);
        return lookup.RequireClientValue(
            clientId,
            DomainErrors.Client,
            () => DomainErrors.ServiceSettings(serviceId, clientId));
    }

    public async Task<ServiceAccessSettings> SetServiceSettingsAsync(
        string clientId,
        string serviceId,
        ServiceAccessSettings settings,
        CancellationToken cancellationToken = default)
    {
        _ = await GetByIdAsync(clientId, cancellationToken);
        await InvalidateAfterAsync(database.SetServiceSettingsAsync(clientId, serviceId, settings, cancellationToken));
        return settings;
    }

    public async Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default)
    {
        _ = await GetByIdAsync(clientId, cancellationToken);
        await InvalidateAfterAsync(database.RemoveServiceSettingsAsync(clientId, serviceId, cancellationToken));
    }

    public async Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>> GetResourcePoolsAsync(
        string clientId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        IReadOnlyList<KeyedEntry<ResourcePoolSettings>> entries = config.ResourcePools
            .Select(entry => new KeyedEntry<ResourcePoolSettings>(entry.Key, entry.Value))
            .ToList();

        return entries.ToPagedResponse(paging);
    }

    public async Task<ResourcePoolSettings> GetResourcePoolSettingsAsync(
        string clientId,
        string poolId,
        CancellationToken cancellationToken = default)
    {
        var lookup = await GetResourcePoolSettingsLookupAsync(clientId, poolId, cancellationToken);
        return lookup.RequireClientValue(
            clientId,
            DomainErrors.Client,
            () => DomainErrors.ResourcePoolSettings(poolId, clientId));
    }

    public async Task<ResourcePoolSettings> SetResourcePoolSettingsAsync(
        string clientId,
        string poolId,
        ResourcePoolSettings settings,
        CancellationToken cancellationToken = default)
    {
        _ = await GetByIdAsync(clientId, cancellationToken);
        await InvalidateAfterAsync(database.SetResourcePoolSettingsAsync(clientId, poolId, settings, cancellationToken));
        return settings;
    }

    public async Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken = default)
    {
        _ = await GetByIdAsync(clientId, cancellationToken);
        await InvalidateAfterAsync(database.RemoveResourcePoolSettingsAsync(clientId, poolId, cancellationToken));
    }

    public async Task<ClientRateLimit> GetGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var lookup = await GetGlobalRateLimitLookupAsync(clientId, cancellationToken);
        return lookup.RequireClientValue(
            clientId,
            DomainErrors.Client,
            () => DomainErrors.ClientGlobalRateLimit(clientId));
    }

    public async Task<ClientRateLimit> SetGlobalRateLimitAsync(
        string clientId,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        await database.UpdateAsync(config with { GlobalRateLimit = rateLimit }, cancellationToken);
        cacheInvalidator.PublishCatalogInvalidation();
        return rateLimit;
    }

    public async Task RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        await database.UpdateAsync(config with { GlobalRateLimit = null }, cancellationToken);
        cacheInvalidator.PublishCatalogInvalidation();
    }

    private Task<ClientConfiguration?> TryGetByIdAsync(string clientId, CancellationToken cancellationToken) =>
        cache.GetOrCreateCatalogAsync(
            $"{CachePrefix}:id:{clientId}",
            token => database.GetByIdAsync(clientId, token),
            cancellationToken);

    private Task<ClientLookup<ServiceAccessSettings>> GetServiceSettingsLookupAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken) =>
        GetSubDocumentAsync(
            clientId,
            $"clients:{clientId}:service-settings:{serviceId}",
            config => new ClientLookup<ServiceAccessSettings>(true, config.Services.GetValueOrDefault(serviceId)),
            cancellationToken);

    private Task<ClientLookup<ResourcePoolSettings>> GetResourcePoolSettingsLookupAsync(
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

    private Task<ClientLookup<ClientRateLimit>> GetGlobalRateLimitLookupAsync(
        string clientId,
        CancellationToken cancellationToken) =>
        GetSubDocumentAsync(
            clientId,
            $"clients:{clientId}:global-rate-limit",
            config => new ClientLookup<ClientRateLimit>(true, config.GlobalRateLimit),
            cancellationToken);

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
        cacheInvalidator.PublishCatalogInvalidation();
    }
}
