using ClientManager.Api.Interfaces;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.RateLimiting;

/// <summary>
/// Approximate sliding window rate limiting strategy. Uses a weighted average of the current
/// and previous fixed window counts to simulate a sliding effect. Unlike a true sliding log
/// (which stores every request timestamp), this approach maintains only two counters and blends
/// them proportionally to how far into the current window the request falls. This is
/// memory-efficient but may slightly over- or under-count near window boundaries.
/// </summary>
public class ApproximateSlidingWindowStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateStore _stateStore;

    /// <summary>
    /// Initializes a new instance of <see cref="ApproximateSlidingWindowStrategy"/>.
    /// </summary>
    /// <param name="stateStore">The state store for rate limit counters.</param>
    public ApproximateSlidingWindowStrategy(IRateLimitStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> EvaluateAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
    {
        var windowSeconds = (long)rateLimit.Window.TotalSeconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var currentWindowNumber = now / windowSeconds;
        var previousWindowNumber = currentWindowNumber - 1;

        var currentWindowKey = $"sliding:{key}:{currentWindowNumber}";
        var previousWindowKey = $"sliding:{key}:{previousWindowNumber}";

        var previousCount = await _stateStore.GetCountAsync(previousWindowKey, cancellationToken);
        var currentCount = await _stateStore.IncrementAsync(currentWindowKey, rateLimit.Window, cancellationToken);

        var windowStart = currentWindowNumber * windowSeconds;
        var elapsedRatio = (double)(now - windowStart) / windowSeconds;
        var weightedCount = previousCount * (1 - elapsedRatio) + currentCount;

        if (weightedCount >= rateLimit.MaxRequests)
        {
            var retryAfter = (int)(windowSeconds - (now - windowStart));

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
            RemainingRequests = Math.Max(0, rateLimit.MaxRequests - (int)Math.Ceiling(weightedCount))
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
        var currentWindowNumber = now / windowSeconds;
        var previousWindowNumber = currentWindowNumber - 1;

        var currentWindowKey = $"sliding:{key}:{currentWindowNumber}";
        var previousWindowKey = $"sliding:{key}:{previousWindowNumber}";

        var previousCount = await _stateStore.GetCountAsync(previousWindowKey, cancellationToken);
        var currentCount = await _stateStore.GetCountAsync(currentWindowKey, cancellationToken);

        var windowStart = currentWindowNumber * windowSeconds;
        var elapsedRatio = (double)(now - windowStart) / windowSeconds;
        var weightedCount = previousCount * (1 - elapsedRatio) + currentCount;

        if (weightedCount >= rateLimit.MaxRequests)
        {
            var retryAfter = (int)(windowSeconds - (now - windowStart));

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
            RemainingRequests = Math.Max(0, rateLimit.MaxRequests - (int)Math.Ceiling(weightedCount))
        };
    }
}
