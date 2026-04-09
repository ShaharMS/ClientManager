using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.StorageApi.Services.Interfaces;

/// <summary>
/// Handles resource-pool catalog operations.
/// </summary>
public interface IResourcePoolCatalogService
{
    Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<ResourcePool?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task CreateAsync(ResourcePool pool, CancellationToken cancellationToken);

    Task<ResourcePool> UpdateAsync(string id, ResourcePool pool, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}