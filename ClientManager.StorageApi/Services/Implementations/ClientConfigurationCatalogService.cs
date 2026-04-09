using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
using ClientManager.StorageApi.Services.Interfaces;
using ClientManager.StorageApi.Utils.Extensions;
using System.Text.Json;

namespace ClientManager.StorageApi.Services.Implementations;

/// <summary>
/// Implements client-configuration catalog behavior on top of the configuration store.
/// </summary>
public class ClientConfigurationCatalogService : IClientConfigurationCatalogService
{
    private readonly IClientConfigurationDatabase _database;
    private readonly IStorageReadCache _cache;

    public ClientConfigurationCatalogService(IClientConfigurationDatabase database, IStorageReadCache cache)
    {
        _database = database;
        _cache = cache;
    }

    public Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync($"clients:search:{Serialize(query)}", token => _database.SearchAsync(query, token), cancellationToken);

    public Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync($"clients:id:{clientId}", token => _database.GetByIdAsync(clientId, token), cancellationToken);

    public async Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken)
    {
        await _database.CreateAsync(configuration, cancellationToken);
        _cache.InvalidateCatalog();
    }

    public async Task<ClientConfiguration> UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken)
    {
        var updated = configuration with { Id = clientId };
        await _database.UpdateAsync(updated, cancellationToken);
        _cache.InvalidateCatalog();
        return updated;
    }

    public async Task DeleteAsync(string clientId, CancellationToken cancellationToken)
    {
        await _database.DeleteAsync(clientId, cancellationToken);
        _cache.InvalidateCatalog();
    }

    public async Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>?> GetServicesAsync(
        string clientId,
        PagedRequest paging,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateCatalogAsync(
            $"clients:{clientId}:services:{paging.Page}:{paging.PageSize}",
            async token =>
            {
                var config = await _database.GetByIdAsync(clientId, token);
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

    public async Task<ClientLookup<ServiceAccessSettings>> GetServiceSettingsAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateCatalogAsync(
            $"clients:{clientId}:service-settings:{serviceId}",
            async token =>
            {
                var config = await _database.GetByIdAsync(clientId, token);
                if (config is null)
                {
                    return new ClientLookup<ServiceAccessSettings>(false, default);
                }

                return new ClientLookup<ServiceAccessSettings>(true, config.Services.GetValueOrDefault(serviceId));
            },
            cancellationToken);
    }

    public async Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken)
    {
        await _database.SetServiceSettingsAsync(clientId, serviceId, settings, cancellationToken);
        _cache.InvalidateCatalog();
    }

    public async Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken)
    {
        await _database.RemoveServiceSettingsAsync(clientId, serviceId, cancellationToken);
        _cache.InvalidateCatalog();
    }

    public async Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>?> GetResourcePoolsAsync(
        string clientId,
        PagedRequest paging,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateCatalogAsync(
            $"clients:{clientId}:resource-pools:{paging.Page}:{paging.PageSize}",
            async token =>
            {
                var config = await _database.GetByIdAsync(clientId, token);
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

    public async Task<ClientLookup<ResourcePoolSettings>> GetResourcePoolSettingsAsync(
        string clientId,
        string poolId,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateCatalogAsync(
            $"clients:{clientId}:resource-pool-settings:{poolId}",
            async token =>
            {
                var config = await _database.GetByIdAsync(clientId, token);
                if (config is null)
                {
                    return new ClientLookup<ResourcePoolSettings>(false, default);
                }

                return new ClientLookup<ResourcePoolSettings>(
                    true,
                    config.ResourcePools.TryGetValue(poolId, out var settings) ? settings : null);
            },
            cancellationToken);
    }

    public Task SetResourcePoolSettingsAsync(
        string clientId,
        string poolId,
        ResourcePoolSettings settings,
        CancellationToken cancellationToken)
    {
        return InvalidateAfterAsync(_database.SetResourcePoolSettingsAsync(clientId, poolId, settings, cancellationToken));
    }

    public Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken)
    {
        return InvalidateAfterAsync(_database.RemoveResourcePoolSettingsAsync(clientId, poolId, cancellationToken));
    }

    public async Task<ClientLookup<ClientRateLimit>> GetGlobalRateLimitAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateCatalogAsync(
            $"clients:{clientId}:global-rate-limit",
            async token =>
            {
                var config = await _database.GetByIdAsync(clientId, token);
                return config is null
                    ? new ClientLookup<ClientRateLimit>(false, default)
                    : new ClientLookup<ClientRateLimit>(true, config.GlobalRateLimit);
            },
            cancellationToken);
    }

    public async Task<bool> SetGlobalRateLimitAsync(
        string clientId,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken)
    {
        var config = await _database.GetByIdAsync(clientId, cancellationToken);
        if (config is null)
        {
            return false;
        }

        await _database.UpdateAsync(config with { GlobalRateLimit = rateLimit }, cancellationToken);
        _cache.InvalidateCatalog();
        return true;
    }

    public async Task<bool> RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken)
    {
        var config = await _database.GetByIdAsync(clientId, cancellationToken);
        if (config is null)
        {
            return false;
        }

        await _database.UpdateAsync(config with { GlobalRateLimit = null }, cancellationToken);
        _cache.InvalidateCatalog();
        return true;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

    private async Task InvalidateAfterAsync(Task operation)
    {
        await operation;
        _cache.InvalidateCatalog();
    }
}