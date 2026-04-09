using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Interfaces.Configuration;

public interface IResourcePoolCatalogClient
{
    Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<ResourcePool?> GetByIdAsync(string poolId, CancellationToken cancellationToken);

    Task CreateAsync(ResourcePool pool, CancellationToken cancellationToken);

    Task UpdateAsync(string poolId, ResourcePool pool, CancellationToken cancellationToken);

    Task DeleteAsync(string poolId, CancellationToken cancellationToken);
}