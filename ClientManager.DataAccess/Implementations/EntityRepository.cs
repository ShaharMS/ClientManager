using ClientManager.DataAccess.Interfaces;

namespace ClientManager.DataAccess.Implementations;

/// <summary>
/// Generic CRUD repository that delegates all storage operations to an <see cref="IDocumentStore"/>.
/// Platform-agnostic — works with any document store implementation.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class EntityRepository<T> : IEntityRepository<T> where T : class
{
    private readonly IDocumentStore _store;
    private readonly string _collection;
    private readonly Func<T, string> _idSelector;

    /// <summary>
    /// Initializes a new instance of <see cref="EntityRepository{T}"/>.
    /// </summary>
    /// <param name="store">The document store to delegate operations to.</param>
    /// <param name="collection">The collection name to store entities in.</param>
    /// <param name="idSelector">A function that extracts the entity's unique identifier.</param>
    public EntityRepository(IDocumentStore store, string collection, Func<T, string> idSelector)
    {
        _store = store;
        _collection = collection;
        _idSelector = idSelector;
    }

    /// <inheritdoc />
    public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _store.GetAsync<T>(_collection, id, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.GetAllAsync<T>(_collection, cancellationToken);

    /// <inheritdoc />
    public Task CreateAsync(T entity, CancellationToken cancellationToken = default) =>
        _store.SetAsync(_collection, _idSelector(entity), entity, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default) =>
        _store.SetAsync(_collection, _idSelector(entity), entity, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(_collection, id, cancellationToken);
}
