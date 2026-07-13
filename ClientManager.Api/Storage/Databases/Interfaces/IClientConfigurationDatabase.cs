using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Storage.Databases.Interfaces;

/// <summary>
/// Database for <see cref="ClientConfiguration"/> documents.
/// </summary>
/// <remarks>
/// <para><strong>Why a dedicated interface</strong></para>
/// <para>
/// Client configurations are full documents edited by the Admin UI and access-check path. They
/// contain nested <see cref="ClientConfiguration.Services"/> dictionaries that must be loaded and
/// saved atomically with the rest of the client record.
/// </para>
/// <para>
/// The generic <see cref="Repositories.Interfaces.IEntityRepository{T}"/> assumes flat entities with
/// a string ID. A standalone database contract keeps client-specific search and count operations
/// explicit for documents that include nested service access settings.
/// </para>
/// </remarks>
public interface IClientConfigurationDatabase
{
    /// <summary>
    /// Retrieves a client configuration by its unique identifier.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The client configuration if found; otherwise <c>null</c>.</returns>
    Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all client configurations.
    /// </summary>
    /// <param name="cancellationToken">Cancels the enumeration early if the caller is shutting down.</param>
    /// <returns>A read-only list of all client configurations.</returns>
    Task<IReadOnlyList<ClientConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for client configurations matching the given query, with server-side filtering and pagination.
    /// </summary>
    /// <param name="query">The query defining filters, sorting, and pagination.</param>
    /// <param name="cancellationToken">Cancels the search if the store is unresponsive.</param>
    /// <returns>The matching configurations and total count (ignoring pagination).</returns>
    Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts client configurations matching the query without materializing full result pages.
    /// </summary>
    /// <param name="query">The query defining filters.</param>
    /// <param name="cancellationToken">Cancels the count if the store is unresponsive.</param>
    /// <returns>The number of matching configurations.</returns>
    Task<long> CountAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new client configuration.
    /// </summary>
    /// <param name="configuration">The client configuration to create.</param>
    /// <param name="cancellationToken">Cancels the write before the document is persisted.</param>
    Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing client configuration.
    /// </summary>
    /// <param name="configuration">The client configuration with updated values.</param>
    /// <param name="cancellationToken">Cancels the update before it is persisted.</param>
    Task UpdateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a client configuration by its unique identifier.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client to delete.</param>
    /// <param name="cancellationToken">Cancels the delete before it completes.</param>
    Task DeleteAsync(string clientId, CancellationToken cancellationToken = default);
}
