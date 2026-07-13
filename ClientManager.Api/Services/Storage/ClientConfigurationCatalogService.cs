using System.Text.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Implements client-configuration catalog behavior on top of the configuration store.
/// </summary>
public sealed class ClientConfigurationCatalogService(
    IClientConfigurationDatabase database,
    IStorageReadCache cache) : IClientConfigurationCatalogService
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
        await database.CreateAsync(configuration, cancellationToken);
        cache.InvalidateCatalog();
        return configuration;
    }

    public async Task<ClientConfiguration> UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _ = await GetByIdAsync(clientId, cancellationToken);
        var updated = configuration with { Id = clientId };
        await database.UpdateAsync(updated, cancellationToken);
        cache.InvalidateCatalog();
        return updated;
    }

    public async Task DeleteAsync(string clientId, CancellationToken cancellationToken = default)
    {
        _ = await GetByIdAsync(clientId, cancellationToken);
        await database.DeleteAsync(clientId, cancellationToken);
        cache.InvalidateCatalog();
    }

    private Task<ClientConfiguration?> TryGetByIdAsync(string clientId, CancellationToken cancellationToken) =>
        cache.GetOrCreateCatalogAsync(
            $"{CachePrefix}:id:{clientId}",
            token => database.GetByIdAsync(clientId, token),
            cancellationToken);
}
