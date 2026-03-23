namespace ClientManager.DataAccess.Bindings.Interfaces;

/// <summary>
/// A generic document store abstraction for CRUD operations on keyed documents.
/// Each platform (JSON file, MongoDB, Redis) implements this once, and all specific
/// repositories compose on top of it.
/// </summary>
public interface IDocumentStore
{
    /// <summary>
    /// Gets a document by its ID from the specified collection.
    /// </summary>
    /// <typeparam name="T">The document type to deserialize.</typeparam>
    /// <param name="collection">The name of the collection to read from.</param>
    /// <param name="id">The unique identifier of the document.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The document if found; otherwise <c>null</c>.</returns>
    Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Gets all documents in the specified collection.
    /// </summary>
    /// <typeparam name="T">The document type to deserialize.</typeparam>
    /// <param name="collection">The name of the collection to read from.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A read-only list of all documents in the collection.</returns>
    Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Creates or overwrites a document with the given ID in the specified collection.
    /// </summary>
    /// <typeparam name="T">The document type to serialize.</typeparam>
    /// <param name="collection">The name of the collection to write to.</param>
    /// <param name="id">The unique identifier of the document.</param>
    /// <param name="document">The document to store.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Deletes a document by its ID from the specified collection.
    /// </summary>
    /// <param name="collection">The name of the collection to delete from.</param>
    /// <param name="id">The unique identifier of the document to delete.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments a counter identified by key. Resets the counter if the window has expired.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="window">The time window after which the counter resets.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The counter value after incrementing.</returns>
    Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current value of a counter.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The current counter value, or <c>0</c> if the counter does not exist.</returns>
    Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a counter to the specified value with an expiry window.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="value">The value to set the counter to.</param>
    /// <param name="window">The time window after which the counter expires.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a counter to zero.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task ResetCounterAsync(string key, CancellationToken cancellationToken = default);
}
