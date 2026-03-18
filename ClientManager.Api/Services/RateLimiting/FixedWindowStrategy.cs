using ClientManager.Api.Interfaces;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.RateLimiting;

/// <summary>
/// Fixed window rate limiting strategy. Counts requests in fixed, non-overlapping time windows.
/// The counter resets at the start of each window.
/// </summary>
public class FixedWindowStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateStore _stateStore;

    /// <summary>
    /// Initializes a new instance of <see cref="FixedWindowStrategy"/>.
    /// </summary>
    /// <param name="stateStore">The state store for rate limit counters.</param>
    public FixedWindowStrategy(IRateLimitStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> EvaluateAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
    {
        var windowSeconds = rateLimit.Window.TotalSeconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowNumber = now / windowSeconds;
        var windowKey = $"fixed:{key}:{windowNumber}";

        var count = await _stateStore.IncrementAsync(windowKey, rateLimit.Window, cancellationToken);

        if (count > rateLimit.MaxRequests)
        {
            var windowStart = windowNumber * windowSeconds;
            var windowEnd = windowStart + windowSeconds;
            var retryAfter = windowEnd - now;

            return new RateLimitResult
            {
                IsAllowed = false,
                RemainingRequests = 0,
                RetryAfterSeconds = (int)retryAfter
            };
        }

        return new RateLimitResult
        {
            IsAllowed = true,
            RemainingRequests = rateLimit.MaxRequests - (int)count
        };
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

        var count = await _stateStore.GetCountAsync(windowKey, cancellationToken);

        if (count >= rateLimit.MaxRequests)
        {
            var windowStart = windowNumber * windowSeconds;
            var windowEnd = windowStart + windowSeconds;
            var retryAfter = (int)(windowEnd - now);

            return new RateLimitResult
            {
                IsAllowed = false,
                RemainingRequests = 0,
                RetryAfterSeconds = retryAfter
            };
        }

        return new RateLimitResult
        {
            IsAllowed = true,
            RemainingRequests = rateLimit.MaxRequests - (int)count
        };
    }
}
