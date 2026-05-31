using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the system-wide catalog of resource pool definitions.
/// Provides search and CRUD access over resource pool documents while keeping the public
/// controller surface decoupled from the storage transport that backs the catalog.
/// </summary>
public interface IResourcePoolCatalogService
{
    /// <summary>
    /// Searches resource pool definitions using the supplied filters, sort, and pagination.
    /// </summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Cancels the catalog search.</param>
    /// <returns>The matching resource pools and total hit count.</returns>
    Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single resource pool definition by its identifier.
    /// </summary>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The matching resource pool definition.</returns>
    Task<ResourcePool> GetByIdAsync(string poolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new resource pool definition.
    /// </summary>
    /// <param name="pool">The resource pool to create.</param>
    /// <param name="cancellationToken">Cancels the create operation.</param>
    /// <returns>The created resource pool definition.</returns>
    Task<ResourcePool> CreateAsync(ResourcePool pool, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing resource pool definition, reconciling its identifier to the route value.
    /// </summary>
    /// <param name="poolId">The unique identifier of the resource pool to update.</param>
    /// <param name="pool">The replacement resource pool definition.</param>
    /// <param name="cancellationToken">Cancels the update operation.</param>
    /// <returns>The updated resource pool definition.</returns>
    Task<ResourcePool> UpdateAsync(string poolId, ResourcePool pool, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a resource pool definition.
    /// </summary>
    /// <param name="poolId">The unique identifier of the resource pool to delete.</param>
    /// <param name="cancellationToken">Cancels the delete operation.</param>
    Task DeleteAsync(string poolId, CancellationToken cancellationToken = default);
}
