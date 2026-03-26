using ClientManager.Api.Services.Implementations.RateLimiting.Strategies;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.Implementations.RateLimiting;

/// <summary>
/// Maps <see cref="RateLimitStrategy"/> enum values to their corresponding
/// <see cref="IRateLimitStrategy"/> implementations.
/// <para>
/// Registered as a singleton during startup with all three strategy instances injected
/// by the DI container. The <see cref="RateLimitService"/> calls <see cref="Resolve"/>
/// at evaluation time to select the correct algorithm for each rate limit configuration.
/// </para>
/// </summary>
public class RateLimitStrategyResolver
{
    private readonly IReadOnlyDictionary<RateLimitStrategy, IRateLimitStrategy> _strategies;

    /// <summary>
    /// Initializes a new instance of <see cref="RateLimitStrategyResolver"/>.
    /// </summary>
    /// <param name="fixedWindow">The fixed window strategy implementation.</param>
    /// <param name="slidingWindow">The approximate sliding window strategy implementation.</param>
    /// <param name="tokenBucket">The token bucket strategy implementation.</param>
    public RateLimitStrategyResolver(
        FixedWindowStrategy fixedWindow,
        ApproximateSlidingWindowStrategy slidingWindow,
        TokenBucketStrategy tokenBucket)
    {
        _strategies = new Dictionary<RateLimitStrategy, IRateLimitStrategy>
        {
            [RateLimitStrategy.FixedWindow] = fixedWindow,
            [RateLimitStrategy.ApproximateSlidingWindow] = slidingWindow,
            [RateLimitStrategy.TokenBucket] = tokenBucket,
        };
    }

    /// <summary>
    /// Resolves the <see cref="IRateLimitStrategy"/> for the given strategy type.
    /// </summary>
    /// <param name="strategy">The rate limit strategy enum value.</param>
    /// <returns>The corresponding strategy implementation.</returns>
    public IRateLimitStrategy Resolve(RateLimitStrategy strategy)
    {
        return _strategies[strategy];
    }
}
