using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Interfaces.Configuration;

// CR: Interface needs documentation. Class should have doc explaining purpose and why it exists somewhat briefly. each method should also have the same documentation - what it does, why it exists, and any important details about behavior/context, with explanitory parameter descriptions.
public interface IResourcePoolCatalogClient
{
    Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<ResourcePool> GetByIdAsync(string poolId, CancellationToken cancellationToken);

    Task CreateAsync(ResourcePool pool, CancellationToken cancellationToken);

    Task UpdateAsync(string poolId, ResourcePool pool, CancellationToken cancellationToken);

    Task DeleteAsync(string poolId, CancellationToken cancellationToken);
}