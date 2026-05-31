using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Internal.Interfaces;

/// <summary>
/// Typed client for the storage-facing global rate-limit catalog.
/// Provides CRUD and search access to standalone global rate-limit definitions so public
/// controllers stay decoupled from the storage API transport.
/// </summary>
public interface IGlobalRateLimitCatalogClient
{
    /// <summary>Searches global rate-limit definitions matching the supplied query.</summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching global rate limits and total hit count.</returns>
    Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Retrieves a single global rate-limit definition by its identifier.</summary>
    /// <param name="id">The global rate-limit identifier to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching global rate-limit definition.</returns>
    Task<GlobalRateLimit> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>Creates a new global rate-limit definition.</summary>
    /// <param name="limit">The global rate limit to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken);

    /// <summary>Replaces an existing global rate-limit definition.</summary>
    /// <param name="id">The global rate-limit identifier being updated.</param>
    /// <param name="limit">The replacement global rate-limit definition.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task UpdateAsync(string id, GlobalRateLimit limit, CancellationToken cancellationToken);

    /// <summary>Deletes a global rate-limit definition.</summary>
    /// <param name="id">The global rate-limit identifier to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
