using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.StorageApi.Services.Interfaces;

/// <summary>
/// Handles global-rate-limit catalog operations.
/// </summary>
public interface IGlobalRateLimitCatalogService
{
    Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<GlobalRateLimit?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken);

    Task<GlobalRateLimit> UpdateAsync(string id, GlobalRateLimit limit, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}