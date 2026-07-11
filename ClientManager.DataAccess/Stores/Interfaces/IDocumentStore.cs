using ClientManager.Shared.Models.Search;

namespace ClientManager.DataAccess.Stores.Interfaces;

/// <summary>
/// Lowest-level storage abstraction in the data-access layer. Every persistence backend
/// (JSON file, MongoDB, Redis) implements this interface once, and all higher-level
/// databases and repositories delegate to it - no caller talks to a store driver directly.
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
///             <see cref="IncrementManyCountersAsync"/>, <see cref="DecrementCounterAsync"/>,
///             <see cref="DecrementManyCountersAsync"/>, <see cref="GetCounterAsync"/>,
///             <see cref="GetManyCountersAsync"/>, <see cref="SetCounterAsync"/>,
///             <see cref="SetManyCountersAsync"/>, <see cref="ResetCounterAsync"/>): simple numeric values with built-in TTL.
///             Used by <see cref="Databases.Interfaces.IRateLimitStateDatabase"/>
///             to track sliding/fixed window counts and token-bucket levels, and by
///             <see cref="Databases.Implementations.ResourceAllocationDatabase"/> to track active allocation totals.
///         </description>
///     </item>
/// </list>
///
/// <para><strong>Why counters live here</strong></para>
/// <para>
///     Rate-limit and allocation counters need atomicity guarantees that vary by backend
///     (e.g. Redis INCR/Lua vs. file-level locking). Pushing them into the same abstraction lets each
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
    /// Gets documents by their IDs from the specified collection.
    /// </summary>
    /// <typeparam name="T">The document type to deserialize.</typeparam>
    /// <param name="collection">The name of the collection to read from.</param>
    /// <param name="ids">The unique identifiers of the documents to fetch.</param>
    /// <param name="cancellationToken">Cancels the batch read, returning control to the caller if the backing store is slow or unresponsive.</param>
    /// <returns>Only the documents that were found; missing IDs are omitted.</returns>
    /// <remarks>
    /// Implementations should avoid full-collection scans when the backend supports direct
    /// key lookup or indexed ID queries.
    /// </remarks>
    Task<IReadOnlyList<T>> GetManyAsync<T>(
        string collection,
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) where T : class;

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
    /// Creates or overwrites multiple documents in the specified collection as one backend-neutral batch.
    /// </summary>
    /// <typeparam name="T">The document type to serialize.</typeparam>
    /// <param name="collection">The name of the collection to write to.</param>
    /// <param name="documents">Documents keyed by their unique identifiers.</param>
    /// <param name="cancellationToken">Cancels the batch write. Backends may keep documents that were already persisted.</param>
    Task SetManyAsync<T>(
        string collection,
        IReadOnlyDictionary<string, T> documents,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Deletes a document by its ID from the specified collection.
    /// </summary>
    /// <param name="collection">The name of the collection to delete from.</param>
    /// <param name="id">The unique identifier of the document to delete.</param>
    /// <param name="cancellationToken">Cancels the delete before it completes.</param>
    Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically evaluates and consumes one token from a token bucket.
    /// </summary>
    Task<(bool IsAllowed, long RemainingTokens, long RetryAfterSeconds)> TryConsumeTokenBucketAsync(
        string tokensKey,
        string lastRefillKey,
        int bucketCapacity,
        int tokensPerRefill,
        long refillIntervalSeconds,
        TimeSpan stateWindow,
        long nowUnixSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments a counter identified by key. Resets the counter if the window has expired.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="window">The time window after which the counter resets.</param>
    /// <param name="cancellationToken">Cancels the increment. If cancelled after the backend applied the write, the counter may already be advanced.</param>
    /// <returns>The counter value after incrementing.</returns>
    Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments multiple counters in one backend-neutral batch operation.
    /// </summary>
    /// <param name="entries">The counter keys mapped to the amount to add and the expiry window to use for newly reset counters.</param>
    /// <param name="cancellationToken">Cancels the batch increment. Counters already written by the backend may remain advanced.</param>
    /// <returns>A dictionary mapping each key to its counter value after incrementing.</returns>
    Task<IReadOnlyDictionary<string, long>> IncrementManyCountersAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically decrements a counter identified by key, flooring at zero.
    /// Unlike a read-modify-write cycle through <see cref="GetCounterAsync"/> and
    /// <see cref="SetCounterAsync"/>, this is safe under concurrent access because
    /// each backend can implement it with a single atomic operation (e.g. Redis DECRBY).
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="cancellationToken">Cancels the decrement before it completes.</param>
    /// <returns>The counter value after decrementing (never negative).</returns>
    Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically decrements multiple counters in one backend-neutral batch operation, flooring each value at zero.
    /// </summary>
    /// <param name="entries">The counter keys mapped to the amount to subtract from each counter.</param>
    /// <param name="cancellationToken">Cancels the batch decrement. Counters already written by the backend may remain decremented.</param>
    /// <returns>A dictionary mapping each key to its counter value after decrementing.</returns>
    Task<IReadOnlyDictionary<string, long>> DecrementManyCountersAsync(
        IReadOnlyDictionary<string, long> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current value of a counter.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="cancellationToken">Cancels the read if the backing store is unresponsive.</param>
    /// <returns>The current counter value, or <c>0</c> if the counter does not exist.</returns>
    Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current values for multiple counters in one backend-neutral batch operation.
    /// </summary>
    /// <param name="keys">The counter keys to read.</param>
    /// <param name="cancellationToken">Cancels the batch read if the backing store is unresponsive.</param>
    /// <returns>A dictionary mapping each requested key to its current value, or <c>0</c> if the counter does not exist.</returns>
    Task<IReadOnlyDictionary<string, long>> GetManyCountersAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets non-zero counters whose keys start with <paramref name="keyPrefix"/>.
    /// </summary>
    Task<IReadOnlyDictionary<string, long>> GetCountersByPrefixAsync(
        string keyPrefix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a counter to the specified value with an expiry window.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="value">The value to set the counter to.</param>
    /// <param name="window">The time window after which the counter expires.</param>
    /// <param name="cancellationToken">Cancels the set before it completes.</param>
    Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets multiple counters in one backend-neutral batch operation.
    /// </summary>
    /// <param name="entries">The counter keys mapped to the value and expiry window to store.</param>
    /// <param name="cancellationToken">Cancels the batch set before all entries are persisted.</param>
    Task SetManyCountersAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a counter to zero.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="cancellationToken">Cancels the reset before it completes.</param>
    Task ResetCounterAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets multiple counters in one batch (one persist on file-backed stores).
    /// </summary>
    Task ResetManyCountersAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes counters whose keys start with <paramref name="keyPrefix"/> when
    /// <paramref name="shouldPurge"/> returns <c>true</c>.
    /// </summary>
    /// <returns>The number of counters removed.</returns>
    Task<int> PurgeCountersByPrefixAsync(
        string keyPrefix,
        Func<string, long, DateTime?, bool> shouldPurge,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for documents matching the given query. Stores may implement native query
    /// translation (e.g. Lucene, MongoDB filters, RediSearch) or fall back to loading all
    /// documents and filtering in memory via <see cref="InMemoryQueryEvaluator"/>.
    /// </summary>
    /// <typeparam name="T">The document type to deserialize.</typeparam>
    /// <param name="collection">The name of the collection to search.</param>
    /// <param name="query">The composable query specifying filters, text search, sort, and pagination.</param>
    /// <param name="cancellationToken">Cancels the search if the backing store is slow or unresponsive.</param>
    /// <returns>A <see cref="SearchResult{T}"/> containing the matching page and total count.</returns>
    Task<SearchResult<T>> SearchAsync<T>(
        string collection,
        DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Counts documents matching the given query without loading them. Useful when only the
    /// count is needed (e.g. active allocation counts, pagination totals).
    /// </summary>
    /// <typeparam name="T">The document type to match against.</typeparam>
    /// <param name="collection">The name of the collection to count in.</param>
    /// <param name="query">The composable query specifying which documents to count.</param>
    /// <param name="cancellationToken">Cancels the count if the backing store is unresponsive.</param>
    /// <returns>The number of documents matching the query.</returns>
    Task<long> CountAsync<T>(
        string collection,
        DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class;
}
