using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Standard catalog CRUD surface shared by client, service, and global-rate-limit catalogs.
/// </summary>
/// <remarks>
/// <para>
/// Catalog services sit between MVC controllers and storage repositories. They apply read-through
/// caching, translate storage failures into domain exceptions, and keep list/search semantics
/// consistent across Admin UI pages.
/// </para>
/// <para>
/// Updates replace the full catalog document with PUT so the Admin UI and API share one edit path
/// per entity.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The catalog entity type.</typeparam>
public interface ICatalogCrudService<TEntity> where TEntity : class
{
    /// <summary>
    /// Searches catalog entries with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass <see cref="DocumentQuery.All"/> for all results.</param>
    /// <param name="cancellationToken">Cancels the search before it completes.</param>
    /// <returns>Matching entries and total count (ignoring pagination).</returns>
    Task<SearchResult<TEntity>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a catalog entry by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entry.</param>
    /// <param name="cancellationToken">Cancels the lookup before it completes.</param>
    /// <returns>The catalog entry.</returns>
    Task<TEntity> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new catalog entry.
    /// </summary>
    /// <param name="entity">The entry to create.</param>
    /// <param name="cancellationToken">Cancels the create before it is persisted.</param>
    /// <returns>The created entry.</returns>
    Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing catalog entry (full document replace).
    /// </summary>
    /// <param name="id">The unique identifier of the entry to update.</param>
    /// <param name="entity">The updated entry.</param>
    /// <param name="cancellationToken">Cancels the update before it is persisted.</param>
    /// <returns>The updated entry.</returns>
    Task<TEntity> UpdateAsync(string id, TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a catalog entry.
    /// </summary>
    /// <param name="id">The unique identifier of the entry to delete.</param>
    /// <param name="cancellationToken">Cancels the delete before it completes.</param>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
