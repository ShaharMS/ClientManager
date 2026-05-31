using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public resource-pool catalog requests onto the storage-facing
/// <see cref="IResourcePoolCatalogClient"/>, reconciling route identifiers on update so the
/// controller never has to reshape the persisted document.
/// </summary>
public class ResourcePoolCatalogService : IResourcePoolCatalogService
{
    private readonly IResourcePoolCatalogClient _resourcePoolCatalogClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourcePoolCatalogService"/>.
    /// </summary>
    /// <param name="resourcePoolCatalogClient">Typed client for the storage-facing resource-pool catalog.</param>
    public ResourcePoolCatalogService(IResourcePoolCatalogClient resourcePoolCatalogClient)
    {
        _resourcePoolCatalogClient = resourcePoolCatalogClient;
    }

    /// <inheritdoc />
    public Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _resourcePoolCatalogClient.SearchAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<ResourcePool> GetByIdAsync(string poolId, CancellationToken cancellationToken = default) =>
        _resourcePoolCatalogClient.GetByIdAsync(poolId, cancellationToken);

    /// <inheritdoc />
    public async Task<ResourcePool> CreateAsync(ResourcePool pool, CancellationToken cancellationToken = default)
    {
        await _resourcePoolCatalogClient.CreateAsync(pool, cancellationToken);
        return pool;
    }

    /// <inheritdoc />
    public async Task<ResourcePool> UpdateAsync(string poolId, ResourcePool pool, CancellationToken cancellationToken = default)
    {
        await _resourcePoolCatalogClient.UpdateAsync(poolId, pool, cancellationToken);
        return pool with { Id = poolId };
    }

    /// <inheritdoc />
    public Task DeleteAsync(string poolId, CancellationToken cancellationToken = default) =>
        _resourcePoolCatalogClient.DeleteAsync(poolId, cancellationToken);
}
