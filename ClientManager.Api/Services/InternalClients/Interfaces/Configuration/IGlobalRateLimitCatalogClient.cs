using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Interfaces.Configuration;

public interface IGlobalRateLimitCatalogClient
{
    Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<GlobalRateLimit?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken);

    Task UpdateAsync(string id, GlobalRateLimit limit, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}