using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using ClientManager.Api.Services.Storage.Models.Exceptions;
using ClientManager.Api.Services.Storage.Interfaces;
using System.Text.Json;

namespace ClientManager.Api.Services.Storage.Implementations;

/// <summary>
/// Implements resource-pool catalog operations.
/// </summary>
public class ResourcePoolCatalogService : IResourcePoolCatalogService
{
    private readonly IEntityRepository<ResourcePool> _repository;
    private readonly IStorageReadCache _cache;

    public ResourcePoolCatalogService(IEntityRepository<ResourcePool> repository, IStorageReadCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync($"resource-pools:search:{JsonSerializer.Serialize(query)}", token => _repository.SearchAsync(query, token), cancellationToken);

    public Task<ResourcePool?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync($"resource-pools:id:{id}", token => _repository.GetByIdAsync(id, token), cancellationToken);

    public async Task CreateAsync(ResourcePool pool, CancellationToken cancellationToken)
    {
        if (await GetByIdAsync(pool.Id, cancellationToken) is not null)
        {
            throw new ResourcePoolAlreadyExistsException(pool.Id);
        }

        await _repository.CreateAsync(pool, cancellationToken);
        _cache.InvalidateCatalog();
    }

    public async Task<ResourcePool> UpdateAsync(string id, ResourcePool pool, CancellationToken cancellationToken)
    {
        if (await GetByIdAsync(id, cancellationToken) is null)
        {
            throw new ResourcePoolNotFoundException(id);
        }

        var updated = pool with { Id = id };
        await _repository.UpdateAsync(updated, cancellationToken);
        _cache.InvalidateCatalog();
        return updated;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (await GetByIdAsync(id, cancellationToken) is null)
        {
            throw new ResourcePoolNotFoundException(id);
        }

        await _repository.DeleteAsync(id, cancellationToken);
        _cache.InvalidateCatalog();
    }
}