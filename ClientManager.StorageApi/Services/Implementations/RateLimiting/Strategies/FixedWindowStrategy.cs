using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.StorageApi.Models.Entities;
using ClientManager.StorageApi.Services.Interfaces;

namespace ClientManager.StorageApi.Services.Implementations.RateLimiting.Strategies;

/// <summary>
/// Fixed-window rate limiting backed by the shared counter store.
/// </summary>
public class FixedWindowStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateDatabase _stateDatabase;

    public FixedWindowStrategy(IRateLimitStateDatabase stateDatabase)
    {
        _stateDatabase = stateDatabase;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> EvaluateAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
    {
        var windowSeconds = (long)rateLimit.Window.TotalSeconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowNumber = now / windowSeconds;
        var windowKey = $"fixed:{key}:{windowNumber}";

        var count = await _stateDatabase.IncrementAsync(windowKey, rateLimit.Window, cancellationToken);
        return CreateResult(rateLimit.MaxRequests, count, windowNumber, windowSeconds, now);
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> PeekAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
    {
        var windowSeconds = (long)rateLimit.Window.TotalSeconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowNumber = now / windowSeconds;
        var windowKey = $"fixed:{key}:{windowNumber}";

        var count = await _stateDatabase.GetCountAsync(windowKey, cancellationToken);
        return CreateResult(rateLimit.MaxRequests, count, windowNumber, windowSeconds, now);
    }

    private static RateLimitResult CreateResult(
        int maxRequests,
        long count,
        long windowNumber,
        long windowSeconds,
        long now)
    {
        if (count < maxRequests)
        {
            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingRequests = maxRequests - (int)count
            };
        }

        var retryAfter = (windowNumber + 1) * windowSeconds - now;
        return new RateLimitResult
        {
            IsAllowed = false,
            RemainingRequests = 0,
            RetryAfterSeconds = (int)retryAfter
        };
    }
}