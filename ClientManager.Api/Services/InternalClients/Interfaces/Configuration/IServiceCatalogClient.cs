using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Interfaces.Configuration;

// CR: Interface needs documentation. Class should have doc explaining purpose and why it exists somewhat briefly. each method should also have the same documentation - what it does, why it exists, and any important details about behavior/context, with explanitory parameter descriptions.
public interface IServiceCatalogClient
{
    Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<Service?> GetByIdAsync(string serviceId, CancellationToken cancellationToken);

    Task CreateAsync(Service service, CancellationToken cancellationToken);

    Task UpdateAsync(string serviceId, Service service, CancellationToken cancellationToken);

    Task DeleteAsync(string serviceId, CancellationToken cancellationToken);
}