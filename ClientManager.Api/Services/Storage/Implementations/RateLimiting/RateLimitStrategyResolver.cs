using ClientManager.Shared.Models.Enums;
using ClientManager.Api.Services.Storage.Implementations.RateLimiting.Strategies;
using ClientManager.Api.Services.Storage.Interfaces;

namespace ClientManager.Api.Services.Storage.Implementations.RateLimiting;

/// <summary>
/// Resolves the concrete algorithm used for a configured rate-limit strategy.
/// </summary>
public class RateLimitStrategyResolver
{
    private readonly IReadOnlyDictionary<RateLimitStrategy, IRateLimitStrategy> _strategies;

    public RateLimitStrategyResolver(
        FixedWindowStrategy fixedWindow,
        ApproximateSlidingWindowStrategy slidingWindow,
        TokenBucketStrategy tokenBucket)
    {
        _strategies = new Dictionary<RateLimitStrategy, IRateLimitStrategy>
        {
            [RateLimitStrategy.FixedWindow] = fixedWindow,
            [RateLimitStrategy.ApproximateSlidingWindow] = slidingWindow,
            [RateLimitStrategy.TokenBucket] = tokenBucket
        };
    }

    /// <summary>
    /// Gets the registered strategy for the requested enum value.
    /// </summary>
    public IRateLimitStrategy Resolve(RateLimitStrategy strategy) => _strategies[strategy];
}