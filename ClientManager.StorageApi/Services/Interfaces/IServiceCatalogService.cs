using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.StorageApi.Services.Interfaces;

/// <summary>
/// Handles service-definition catalog operations.
/// </summary>
public interface IServiceCatalogService
{
    Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<Service?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task CreateAsync(Service service, CancellationToken cancellationToken);

    Task<Service> UpdateAsync(string id, Service service, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}