using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Storage.Repositories.Interfaces;

/// <summary>
/// Standard CRUD contract for entities identified by a string key. Provides the baseline
/// operations that catalog entities need (services and global rate limits).
///
/// <para>
///     Entities with richer query requirements extend this interface with domain-specific
///     methods (e.g. <see cref="Databases.Interfaces.IGlobalRateLimitDatabase"/> adds
///     service-ID lookups). Nested client configuration documents use their own
///     <see cref="Databases.Interfaces.IClientConfigurationDatabase"/> contract.
/// </para>
/// </summary>
/// <typeparam name="T">The entity type. Must be a reference type with a string <c>Id</c> property
/// by convention.</typeparam>
public interface IEntityRepository<T> where T : class
{
    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The entity if found; otherwise <c>null</c>.</returns>
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entities in the repository.
    /// </summary>
    /// <param name="cancellationToken">Cancels the enumeration early if the caller is shutting down.</param>
    /// <returns>A read-only list of all entities.</returns>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="cancellationToken">Cancels the write before the entity is persisted.</param>
    Task CreateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    /// <param name="cancellationToken">Cancels the update before it is persisted.</param>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="cancellationToken">Cancels the delete before it completes.</param>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for entities matching the given query, with server-side filtering and pagination.
    /// </summary>
    /// <param name="query">The query defining filters, sorting, and pagination.</param>
    /// <param name="cancellationToken">Cancels the search if the store is unresponsive.</param>
    /// <returns>The matching entities and total count (ignoring pagination).</returns>
    Task<SearchResult<T>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entities matching the query without materializing full result pages.
    /// </summary>
    Task<long> CountAsync(DocumentQuery query, CancellationToken cancellationToken = default);
}
