using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using ClientManager.StorageApi.Models.Exceptions;
using ClientManager.StorageApi.Services.Interfaces;
using System.Text.Json;

namespace ClientManager.StorageApi.Services.Implementations;

/// <summary>
/// Implements global-rate-limit catalog operations.
/// </summary>
public class GlobalRateLimitCatalogService : IGlobalRateLimitCatalogService
{
    private readonly IGlobalRateLimitDatabase _database;
    private readonly IStorageReadCache _cache;

    public GlobalRateLimitCatalogService(IGlobalRateLimitDatabase database, IStorageReadCache cache)
    {
        _database = database;
        _cache = cache;
    }

    public Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync($"global-rate-limits:search:{JsonSerializer.Serialize(query)}", token => _database.SearchAsync(query, token), cancellationToken);

    public Task<GlobalRateLimit?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync($"global-rate-limits:id:{id}", token => _database.GetByIdAsync(id, token), cancellationToken);

    public async Task CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        var existing = await _database.GetByTargetAsync(limit.TargetId, limit.TargetType, cancellationToken);
        if (existing is not null)
        {
            throw new GlobalRateLimitAlreadyExistsException(limit.TargetId, limit.TargetType);
        }

        await _database.CreateAsync(limit, cancellationToken);
        _cache.InvalidateCatalog();
    }

    public async Task<GlobalRateLimit> UpdateAsync(string id, GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        if (await GetByIdAsync(id, cancellationToken) is null)
        {
            throw new GlobalRateLimitNotFoundException(id);
        }

        var updated = limit with { Id = id };
        await _database.UpdateAsync(updated, cancellationToken);
        _cache.InvalidateCatalog();
        return updated;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (await GetByIdAsync(id, cancellationToken) is null)
        {
            throw new GlobalRateLimitNotFoundException(id);
        }

        await _database.DeleteAsync(id, cancellationToken);
        _cache.InvalidateCatalog();
    }
}