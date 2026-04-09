using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using ClientManager.StorageApi.Services.Interfaces;
using System.Text.Json;

namespace ClientManager.StorageApi.Services.Implementations;

/// <summary>
/// Implements service-definition catalog operations.
/// </summary>
public class ServiceCatalogService : IServiceCatalogService
{
    private readonly IEntityRepository<Service> _repository;
    private readonly IStorageReadCache _cache;

    public ServiceCatalogService(IEntityRepository<Service> repository, IStorageReadCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync($"services:search:{JsonSerializer.Serialize(query)}", token => _repository.SearchAsync(query, token), cancellationToken);

    public Task<Service?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync($"services:id:{id}", token => _repository.GetByIdAsync(id, token), cancellationToken);

    public async Task CreateAsync(Service service, CancellationToken cancellationToken)
    {
        await _repository.CreateAsync(service, cancellationToken);
        _cache.InvalidateCatalog();
    }

    public async Task<Service> UpdateAsync(string id, Service service, CancellationToken cancellationToken)
    {
        var updated = service with { Id = id };
        await _repository.UpdateAsync(updated, cancellationToken);
        _cache.InvalidateCatalog();
        return updated;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(id, cancellationToken);
        _cache.InvalidateCatalog();
    }
}