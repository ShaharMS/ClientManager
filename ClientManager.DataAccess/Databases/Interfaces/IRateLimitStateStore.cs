namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Thin abstraction over the atomic counter operations that rate-limit strategies need.
/// Each call maps 1:1 to an <see cref="Stores.Interfaces.IDocumentStore"/> counter method,
/// keeping the strategy implementations decoupled from the storage backend.
///
/// <para>
///     The <c>key</c> parameter in every method is a composite string built by the calling
///     strategy (e.g. <c>"{clientId}:{serviceId}"</c> for per-client limits, or
///     <c>"global:service:{serviceId}"</c> for system-wide limits). The <c>window</c>
///     parameter doubles as the counter's TTL - once the window elapses, the counter
///     auto-expires so stale state never lingers.
/// </para>
/// </summary>
public interface IRateLimitStateStore
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
}
