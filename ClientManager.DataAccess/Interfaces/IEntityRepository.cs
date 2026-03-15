namespace ClientManager.DataAccess.Interfaces;

/// <summary>
/// Generic repository interface for CRUD operations on entities.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IEntityRepository<T> where T : class
{
    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The entity if found; otherwise <c>null</c>.</returns>
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entities in the repository.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A read-only list of all entities.</returns>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task CreateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
