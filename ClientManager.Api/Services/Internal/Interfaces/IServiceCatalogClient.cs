using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Internal.Interfaces;

/// <summary>
/// Typed client for the storage-facing service catalog.
/// Provides CRUD and search access to service definitions so public controllers
/// stay decoupled from the storage API transport.
/// </summary>
public interface IServiceCatalogClient
{
    /// <summary>Searches service definitions matching the supplied query.</summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching services and total hit count.</returns>
    Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Retrieves a single service definition by its identifier.</summary>
    /// <param name="serviceId">The service identifier to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching service definition.</returns>
    Task<Service> GetByIdAsync(string serviceId, CancellationToken cancellationToken);

    /// <summary>Creates a new service definition.</summary>
    /// <param name="service">The service to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task CreateAsync(Service service, CancellationToken cancellationToken);

    /// <summary>Replaces an existing service definition.</summary>
    /// <param name="serviceId">The service identifier being updated.</param>
    /// <param name="service">The replacement service definition.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task UpdateAsync(string serviceId, Service service, CancellationToken cancellationToken);

    /// <summary>Deletes a service definition.</summary>
    /// <param name="serviceId">The service identifier to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task DeleteAsync(string serviceId, CancellationToken cancellationToken);
}
