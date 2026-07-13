using ClientManager.Api.Storage.Stores.Interfaces;
using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Storage.Databases.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IRateLimitStateDatabase"/>.
/// Delegates all counter operations to <see cref="IDocumentStore"/>.
/// </summary>
/// <param name="store">The document store to delegate counter operations to.</param>
public class RateLimitStateDatabase(IDocumentStore store) : IRateLimitStateDatabase
{
    /// <inheritdoc />
    public Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken = default) =>
        store.IncrementCounterAsync(key, window, cancellationToken);

    /// <inheritdoc />
    public Task<long> GetCountAsync(string key, CancellationToken cancellationToken = default) =>
        store.GetCounterAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task SetCountAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default) =>
        store.SetCounterAsync(key, value, window, cancellationToken);

    /// <inheritdoc />
    public Task ResetAsync(string key, CancellationToken cancellationToken = default) =>
        store.ResetCounterAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, long>> GetMultipleCountsAsync(
        IEnumerable<string> keys, CancellationToken cancellationToken = default) =>
        store.GetManyCountersAsync(keys, cancellationToken);

    /// <inheritdoc />
    public Task SetMultipleCountsAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken = default) =>
        store.SetManyCountersAsync(entries, cancellationToken);

    /// <inheritdoc />
    public async Task<TokenBucketConsumeResult> TryConsumeTokenBucketAsync(
        string tokensKey,
        string lastRefillKey,
        int bucketCapacity,
        int tokensPerRefill,
        long refillIntervalSeconds,
        TimeSpan stateWindow,
        long nowUnixSeconds,
        CancellationToken cancellationToken = default)
    {
        var result = await store.TryConsumeTokenBucketAsync(
            tokensKey,
            lastRefillKey,
            bucketCapacity,
            tokensPerRefill,
            refillIntervalSeconds,
            stateWindow,
            nowUnixSeconds,
            cancellationToken);

        return new TokenBucketConsumeResult(
            result.IsAllowed,
            (int)Math.Max(0, result.RemainingTokens),
            (int)Math.Max(0, result.RetryAfterSeconds));
    }
}
