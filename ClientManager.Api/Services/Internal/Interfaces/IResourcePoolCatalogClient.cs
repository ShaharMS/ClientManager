using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Internal.Interfaces;

/// <summary>
/// Typed client for the storage-facing resource-pool catalog.
/// Provides CRUD and search access to resource-pool definitions so public controllers
/// stay decoupled from the storage API transport.
/// </summary>
public interface IResourcePoolCatalogClient
{
    /// <summary>Searches resource-pool definitions matching the supplied query.</summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching resource pools and total hit count.</returns>
    Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Retrieves a single resource-pool definition by its identifier.</summary>
    /// <param name="poolId">The resource-pool identifier to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching resource-pool definition.</returns>
    Task<ResourcePool> GetByIdAsync(string poolId, CancellationToken cancellationToken);

    /// <summary>Creates a new resource-pool definition.</summary>
    /// <param name="pool">The resource pool to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task CreateAsync(ResourcePool pool, CancellationToken cancellationToken);

    /// <summary>Replaces an existing resource-pool definition.</summary>
    /// <param name="poolId">The resource-pool identifier being updated.</param>
    /// <param name="pool">The replacement resource-pool definition.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task UpdateAsync(string poolId, ResourcePool pool, CancellationToken cancellationToken);

    /// <summary>Deletes a resource-pool definition.</summary>
    /// <param name="poolId">The resource-pool identifier to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task DeleteAsync(string poolId, CancellationToken cancellationToken);
}
