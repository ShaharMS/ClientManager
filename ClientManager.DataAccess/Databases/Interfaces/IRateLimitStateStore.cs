namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Store for rate limit counters with windowed increment support.
/// </summary>
public interface IRateLimitStateStore
{
    /// <summary>
    /// Atomically increments the counter for the given key within the specified window.
    /// </summary>
    /// <param name="key">The rate limit counter key.</param>
    /// <param name="window">The time window after which the counter resets.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The counter value after incrementing.</returns>
    Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current count for the given key.
    /// </summary>
    /// <param name="key">The rate limit counter key.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The current counter value, or <c>0</c> if the key does not exist.</returns>
    Task<long> GetCountAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the counter for the given key to a specific value.
    /// </summary>
    /// <param name="key">The rate limit counter key.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="window">The time window after which the counter expires.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task SetCountAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the counter for the given key to zero.
    /// </summary>
    /// <param name="key">The rate limit counter key to reset.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task ResetAsync(string key, CancellationToken cancellationToken = default);
}
