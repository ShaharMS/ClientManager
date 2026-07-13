using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Tests.Helpers;

/// <summary>
/// In-memory <see cref="IRateLimitStateDatabase"/> for focused strategy unit tests.
/// </summary>
public sealed class InMemoryRateLimitStateDatabase : IRateLimitStateDatabase
{
    private readonly Dictionary<string, long> _counts = new(StringComparer.Ordinal);

    public Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var next = _counts.TryGetValue(key, out var current) ? current + 1 : 1;
        _counts[key] = next;
        return Task.FromResult(next);
    }

    public Task<long> GetCountAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_counts.TryGetValue(key, out var value) ? value : 0L);

    public Task SetCountAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default)
    {
        _counts[key] = value;
        return Task.CompletedTask;
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        _counts.Remove(key);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, long>> GetMultipleCountsAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            result[key] = _counts.TryGetValue(key, out var value) ? value : 0L;
        }

        return Task.FromResult<IReadOnlyDictionary<string, long>>(result);
    }

    public Task SetMultipleCountsAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        foreach (var (key, (value, _)) in entries)
        {
            _counts[key] = value;
        }

        return Task.CompletedTask;
    }

    public Task<TokenBucketConsumeResult> TryConsumeTokenBucketAsync(
        string tokensKey,
        string lastRefillKey,
        int bucketCapacity,
        int tokensPerRefill,
        long refillIntervalSeconds,
        TimeSpan stateWindow,
        long nowUnixSeconds,
        CancellationToken cancellationToken = default)
    {
        _ = GetCountAsync(lastRefillKey, cancellationToken).Result;
        var tokens = GetCountAsync(tokensKey, cancellationToken).Result;
        if (tokens == 0 && !_counts.ContainsKey(lastRefillKey))
        {
            var initial = bucketCapacity - 1;
            _counts[tokensKey] = initial;
            _counts[lastRefillKey] = nowUnixSeconds;
            return Task.FromResult(new TokenBucketConsumeResult(true, (int)initial, 0));
        }

        if (tokens <= 0)
        {
            return Task.FromResult(new TokenBucketConsumeResult(false, 0, (int)refillIntervalSeconds));
        }

        _counts[tokensKey] = tokens - 1;
        return Task.FromResult(new TokenBucketConsumeResult(true, (int)(tokens - 1), 0));
    }
}
