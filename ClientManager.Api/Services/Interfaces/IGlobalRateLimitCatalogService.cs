using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the system-wide catalog of standalone global rate-limit definitions.
/// Provides search and CRUD access over global rate-limit documents while keeping the public
/// controller surface decoupled from the storage transport that backs the catalog.
/// </summary>
public interface IGlobalRateLimitCatalogService
{
    /// <summary>
    /// Searches global rate-limit definitions using the supplied filters, sort, and pagination.
    /// </summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Cancels the catalog search.</param>
    /// <returns>The matching global rate limits and total hit count.</returns>
    Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single global rate-limit definition by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The matching global rate-limit definition.</returns>
    Task<GlobalRateLimit> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new global rate-limit definition.
    /// </summary>
    /// <param name="limit">The global rate limit to create.</param>
    /// <param name="cancellationToken">Cancels the create operation.</param>
    /// <returns>The created global rate-limit definition.</returns>
    Task<GlobalRateLimit> CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing global rate-limit definition, reconciling its identifier to the route value.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit to update.</param>
    /// <param name="limit">The replacement global rate-limit definition.</param>
    /// <param name="cancellationToken">Cancels the update operation.</param>
    /// <returns>The updated global rate-limit definition.</returns>
    Task<GlobalRateLimit> UpdateAsync(string id, GlobalRateLimit limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a global rate-limit definition.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit to delete.</param>
    /// <param name="cancellationToken">Cancels the delete operation.</param>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
