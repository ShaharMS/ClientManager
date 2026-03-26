using ClientManager.Api.Models.Entities;
using ClientManager.Api.Services.Interfaces;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Implementations.RateLimiting.Strategies;

/// <summary>
/// Approximate sliding window rate limiting strategy. Uses a weighted average of the current
/// and previous fixed window counts to simulate a sliding effect. Unlike a true sliding log
/// (which stores every request timestamp), this approach maintains only two counters and blends
/// them proportionally to how far into the current window the request falls.
/// <para>
/// This is memory-efficient but may slightly over- or under-count near window boundaries.
/// It eliminates the boundary-burst problem of the fixed window strategy while keeping
/// storage requirements minimal (two counter keys per rate limit).
/// </para>
/// </summary>
public class ApproximateSlidingWindowStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateDatabase _stateStore;

    /// <summary>
    /// Initializes a new instance of <see cref="ApproximateSlidingWindowStrategy"/>.
    /// </summary>
    /// <param name="stateStore">The state database for rate limit counters.</param>
    public ApproximateSlidingWindowStrategy(IRateLimitStateDatabase stateStore)
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

        // Batch read: 1 round trip instead of 2
        var counts = await _stateStore.GetMultipleCountsAsync([previousWindowKey, currentWindowKey], cancellationToken);
        var previousCount = counts[previousWindowKey];
        var currentCount = counts[currentWindowKey];

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
