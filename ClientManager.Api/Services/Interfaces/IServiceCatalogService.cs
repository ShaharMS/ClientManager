using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the system-wide catalog of service definitions.
/// Provides search and CRUD access over service documents while keeping the public
/// controller surface decoupled from the storage transport that backs the catalog.
/// </summary>
public interface IServiceCatalogService
{
    /// <summary>
    /// Searches service definitions using the supplied filters, sort, and pagination.
    /// </summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Cancels the catalog search.</param>
    /// <returns>The matching services and total hit count.</returns>
    Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single service definition by its identifier.
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The matching service definition.</returns>
    Task<Service> GetByIdAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new service definition.
    /// </summary>
    /// <param name="service">The service to create.</param>
    /// <param name="cancellationToken">Cancels the create operation.</param>
    /// <returns>The created service definition.</returns>
    Task<Service> CreateAsync(Service service, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing service definition, reconciling its identifier to the route value.
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service to update.</param>
    /// <param name="service">The replacement service definition.</param>
    /// <param name="cancellationToken">Cancels the update operation.</param>
    /// <returns>The updated service definition.</returns>
    Task<Service> UpdateAsync(string serviceId, Service service, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a service definition.
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service to delete.</param>
    /// <param name="cancellationToken">Cancels the delete operation.</param>
    Task DeleteAsync(string serviceId, CancellationToken cancellationToken = default);
}
