using ClientManager.Api.Models.Entities;
using ClientManager.Api.Services.Interfaces;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Implementations.RateLimiting.Strategies;

/// <summary>
/// Fixed window rate limiting strategy. Counts requests in fixed, non-overlapping time windows.
/// The counter resets at the start of each window.
/// <para>
/// Simple and predictable, but susceptible to burst traffic at window boundaries
/// (a client can make <c>MaxRequests</c> at the end of one window and another
/// <c>MaxRequests</c> at the start of the next, doubling throughput momentarily).
/// Use <see cref="ApproximateSlidingWindowStrategy"/> to mitigate this.
/// </para>
/// </summary>
public class FixedWindowStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateDatabase _stateStore;

    /// <summary>
    /// Initializes a new instance of <see cref="FixedWindowStrategy"/>.
    /// </summary>
    /// <param name="stateStore">The state database for rate limit counters.</param>
    public FixedWindowStrategy(IRateLimitStateDatabase stateStore)
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
