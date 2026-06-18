using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Thin abstraction over the atomic counter operations that rate-limit strategies need.
/// Each call maps 1:1 to an <see cref="IDocumentStore"/> counter method,
/// keeping the strategy implementations decoupled from the storage backend.
///
/// <para>
///     The <c>key</c> parameter in every method is an identifier of the counter, with composition
///     influenced by the specific rate limit enforcer's implementation.
/// </para>
/// </summary>
public interface IRateLimitStateDatabase
{
    /// <summary>
    /// Atomically increments the counter for the given key within the specified window.
    /// </summary>
    /// <param name="key">The rate limit counter key.</param>
    /// <param name="window">The time window after which the counter resets.</param>
    /// <param name="cancellationToken">Cancels the increment. If cancelled after the backend applied the write, the counter may already be advanced.</param>
    /// <returns>The counter value after incrementing.</returns>
    Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current count for the given key.
    /// </summary>
    /// <param name="key">The rate limit counter key.</param>
    /// <param name="cancellationToken">Cancels the read if the backing store is unresponsive.</param>
    /// <returns>The current counter value, or <c>0</c> if the key does not exist.</returns>
    Task<long> GetCountAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the counter for the given key to a specific value.
    /// </summary>
    /// <param name="key">The rate limit counter key.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="window">The time window after which the counter expires.</param>
    /// <param name="cancellationToken">Cancels the set before it completes.</param>
    Task SetCountAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the counter for the given key to zero.
    /// </summary>
    /// <param name="key">The rate limit counter key to reset.</param>
    /// <param name="cancellationToken">Cancels the reset before it completes.</param>
    Task ResetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads multiple counters in a single logical operation, reducing round trips for
    /// strategies like <c>TokenBucketStrategy</c> that need several keys per evaluation.
    /// </summary>
    /// <param name="keys">The rate limit counter keys to read.</param>
    /// <param name="cancellationToken">Cancels the batch read if the store is unresponsive.</param>
    /// <returns>A dictionary mapping each key to its current value (0 if the key does not exist).</returns>
    Task<IReadOnlyDictionary<string, long>> GetMultipleCountsAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes multiple counters in a single logical operation, reducing round trips for
    /// strategies like <c>TokenBucketStrategy</c> that update several keys per evaluation.
    /// </summary>
    /// <param name="entries">A dictionary mapping each key to its new value and expiry window.</param>
    /// <param name="cancellationToken">Cancels the batch write before all entries are persisted.</param>
    Task SetMultipleCountsAsync(IReadOnlyDictionary<string, (long value, TimeSpan window)> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes one token from a token bucket when allowed.
    /// </summary>
    Task<TokenBucketConsumeResult> TryConsumeTokenBucketAsync(
        string tokensKey,
        string lastRefillKey,
        int bucketCapacity,
        int tokensPerRefill,
        long refillIntervalSeconds,
        TimeSpan stateWindow,
        long nowUnixSeconds,
        CancellationToken cancellationToken = default);
}
