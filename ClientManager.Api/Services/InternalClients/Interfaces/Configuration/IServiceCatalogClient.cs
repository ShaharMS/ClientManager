using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Interfaces.Configuration;

public interface IServiceCatalogClient
{
    Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<Service?> GetByIdAsync(string serviceId, CancellationToken cancellationToken);

    Task CreateAsync(Service service, CancellationToken cancellationToken);

    Task UpdateAsync(string serviceId, Service service, CancellationToken cancellationToken);

    Task DeleteAsync(string serviceId, CancellationToken cancellationToken);
}