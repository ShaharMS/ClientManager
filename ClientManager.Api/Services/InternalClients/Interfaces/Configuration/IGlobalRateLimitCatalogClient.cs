using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Interfaces.Configuration;

// CR: Interface needs documentation. Class should have doc explaining purpose and why it exists somewhat briefly. each method should also have the same documentation - what it does, why it exists, and any important details about behavior/context, with explanitory parameter descriptions.
public interface IGlobalRateLimitCatalogClient
{
    Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<GlobalRateLimit> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken);

    Task UpdateAsync(string id, GlobalRateLimit limit, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}