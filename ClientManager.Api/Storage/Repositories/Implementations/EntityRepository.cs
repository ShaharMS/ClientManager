using ClientManager.Api.Storage.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using ClientManager.Api.Storage.Repositories.Interfaces;

namespace ClientManager.Api.Storage.Repositories.Implementations;

/// <summary>
/// Generic CRUD repository that delegates all storage operations to an <see cref="IDocumentStore"/>.
/// Platform-agnostic - works with any document store implementation.
/// </summary>
/// <typeparam name="T">The type of the stored entity. Must be a class instance</typeparam>
/// <param name="store">The document store to delegate operations to.</param>
/// <param name="collection">The collection name to store entities in.</param>
/// <param name="idSelector">A function that extracts the entity's unique identifier.</param>
public class EntityRepository<T>(IDocumentStore store, string collection, Func<T, string> idSelector)
    : IEntityRepository<T> where T : class
{
    /// <inheritdoc />
    public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        store.GetAsync<T>(collection, id, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        store.GetAllAsync<T>(collection, cancellationToken);

    /// <inheritdoc />
    public Task CreateAsync(T entity, CancellationToken cancellationToken = default) =>
        store.SetAsync(collection, idSelector(entity), entity, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default) =>
        store.SetAsync(collection, idSelector(entity), entity, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        store.DeleteAsync(collection, id, cancellationToken);

    /// <inheritdoc />
    public Task<SearchResult<T>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        store.SearchAsync<T>(collection, query, cancellationToken);

    /// <inheritdoc />
    public Task<long> CountAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        store.CountAsync<T>(collection, query, cancellationToken);
}
