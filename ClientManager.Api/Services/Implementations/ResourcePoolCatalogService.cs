using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using StorageResourcePoolCatalogService = ClientManager.Api.Services.Storage.Interfaces.IResourcePoolCatalogService;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public resource-pool catalog requests onto the in-process storage resource-pool catalog,
/// translating an absent pool into a <see cref="ResourcePoolNotFoundException"/> and reconciling
/// route identifiers on update so the controller never has to reshape the persisted document.
/// </summary>
public class ResourcePoolCatalogService : IResourcePoolCatalogService
{
    private readonly StorageResourcePoolCatalogService _resourcePoolCatalogService;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourcePoolCatalogService"/>.
    /// </summary>
    /// <param name="resourcePoolCatalogService">In-process storage resource-pool catalog.</param>
    public ResourcePoolCatalogService(StorageResourcePoolCatalogService resourcePoolCatalogService)
    {
        _resourcePoolCatalogService = resourcePoolCatalogService;
    }

    /// <inheritdoc />
    public Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _resourcePoolCatalogService.SearchAsync(query, cancellationToken);

    /// <inheritdoc />
    public async Task<ResourcePool> GetByIdAsync(string poolId, CancellationToken cancellationToken = default) =>
        await _resourcePoolCatalogService.GetByIdAsync(poolId, cancellationToken)
            ?? throw new ResourcePoolNotFoundException(poolId);

    /// <inheritdoc />
    public async Task<ResourcePool> CreateAsync(ResourcePool pool, CancellationToken cancellationToken = default)
    {
        await _resourcePoolCatalogService.CreateAsync(pool, cancellationToken);
        return pool;
    }

    /// <inheritdoc />
    public Task<ResourcePool> UpdateAsync(string poolId, ResourcePool pool, CancellationToken cancellationToken = default) =>
        _resourcePoolCatalogService.UpdateAsync(poolId, pool, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string poolId, CancellationToken cancellationToken = default) =>
        _resourcePoolCatalogService.DeleteAsync(poolId, cancellationToken);
}
