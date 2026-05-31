using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the lifecycle of client configuration documents.
/// Owns search and top-level CRUD over the client configuration aggregate, leaving the nested
/// service, resource-pool, and global rate-limit sub-resources to their dedicated services.
/// </summary>
public interface IClientConfigurationService
{
    /// <summary>
    /// Searches client configurations using the supplied filters, sort, and pagination.
    /// </summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The matching client configurations and total hit count.</returns>
    Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single client configuration by its identifier.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The matching client configuration.</returns>
    Task<ClientConfiguration> GetByIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new client configuration document.
    /// </summary>
    /// <param name="configuration">The client configuration to create.</param>
    /// <param name="cancellationToken">Cancels the create operation.</param>
    /// <returns>The created client configuration.</returns>
    Task<ClientConfiguration> CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing client configuration, reconciling its identifier to the route value.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client to update.</param>
    /// <param name="configuration">The replacement client configuration.</param>
    /// <param name="cancellationToken">Cancels the update operation.</param>
    /// <returns>The updated client configuration.</returns>
    Task<ClientConfiguration> UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a client configuration document.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client to delete.</param>
    /// <param name="cancellationToken">Cancels the delete operation.</param>
    Task DeleteAsync(string clientId, CancellationToken cancellationToken = default);
}
