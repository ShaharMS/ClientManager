namespace ClientManager.DataAccess.Stores.Interfaces;

/// <summary>
/// Lowest-level storage abstraction in the data-access layer. Every persistence backend
/// (JSON file, MongoDB, Redis) implements this interface once, and all higher-level
/// repositories delegate to it - no repository talks to a database driver directly.
///
/// <para><strong>Two responsibilities in one surface</strong></para>
/// <para>
///     The interface exposes two distinct capabilities:
/// </para>
/// <list type="number">
///     <item>
///         <description>
///             <strong>Document CRUD</strong> (<see cref="GetAsync{T}"/>,
///             <see cref="GetAllAsync{T}"/>, <see cref="SetAsync{T}"/>,
///             <see cref="DeleteAsync"/>): keyed JSON-serializable documents grouped by
///             collection name. Used by entity repositories for configuration, allocations,
///             rate-limit rules, and usage snapshots.
///         </description>
///     </item>
///     <item>
///         <description>
///             <strong>Atomic counters</strong> (<see cref="IncrementCounterAsync"/>,
///             <see cref="GetCounterAsync"/>, <see cref="SetCounterAsync"/>,
///             <see cref="ResetCounterAsync"/>): simple numeric values with built-in TTL.
///             Used exclusively by <see cref="Databases.Interfaces.IRateLimitStateStore"/>
///             to track sliding/fixed window counts and token-bucket levels.
///         </description>
///     </item>
/// </list>
///
/// <para><strong>Why counters live here</strong></para>
/// <para>
///     Rate-limit counters need atomicity guarantees that vary by backend (e.g. Redis
///     INCR vs. file-level locking). Pushing them into the same abstraction lets each
///     backend implement the atomic-increment semantics that are natural for its storage
///     engine, rather than forcing a read-modify-write cycle through the document API.
/// </para>
/// </summary>
public interface IDocumentStore
{
    /// <summary>
    /// Gets a document by its ID from the specified collection.
    /// </summary>
    /// <typeparam name="T">The document type to deserialize.</typeparam>
    /// <param name="collection">The name of the collection to read from.</param>
    /// <param name="id">The unique identifier of the document.</param>
    /// <param name="cancellationToken">Cancels the read, returning control to the caller if the backing store is slow or unresponsive.</param>
    /// <returns>The document if found; otherwise <c>null</c>.</returns>
    Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Gets all documents in the specified collection.
    /// </summary>
    /// <typeparam name="T">The document type to deserialize.</typeparam>
    /// <param name="collection">The name of the collection to read from.</param>
    /// <param name="cancellationToken">Cancels the enumeration early - useful when the collection is large and the caller is shutting down.</param>
    /// <returns>A read-only list of all documents in the collection.</returns>
    Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Creates or overwrites a document with the given ID in the specified collection.
    /// </summary>
    /// <typeparam name="T">The document type to serialize.</typeparam>
    /// <param name="collection">The name of the collection to write to.</param>
    /// <param name="id">The unique identifier of the document.</param>
    /// <param name="document">The document to store.</param>
    /// <param name="cancellationToken">Cancels the write. Depending on the backend, a partially completed write may or may not be rolled back.</param>
    Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Deletes a document by its ID from the specified collection.
    /// </summary>
    /// <param name="collection">The name of the collection to delete from.</param>
    /// <param name="id">The unique identifier of the document to delete.</param>
    /// <param name="cancellationToken">Cancels the delete before it completes.</param>
    Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments a counter identified by key. Resets the counter if the window has expired.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="window">The time window after which the counter resets.</param>
    /// <param name="cancellationToken">Cancels the increment. If cancelled after the backend applied the write, the counter may already be advanced.</param>
    /// <returns>The counter value after incrementing.</returns>
    Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current value of a counter.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="cancellationToken">Cancels the read if the backing store is unresponsive.</param>
    /// <returns>The current counter value, or <c>0</c> if the counter does not exist.</returns>
    Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a counter to the specified value with an expiry window.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="value">The value to set the counter to.</param>
    /// <param name="window">The time window after which the counter expires.</param>
    /// <param name="cancellationToken">Cancels the set before it completes.</param>
    Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a counter to zero.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="cancellationToken">Cancels the reset before it completes.</param>
    Task ResetCounterAsync(string key, CancellationToken cancellationToken = default);
}
