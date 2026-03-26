using ClientManager.Api.Models.Entities;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Defines a rate limiting algorithm that evaluates whether a request should be allowed.
/// <para>
/// Each implementation encapsulates a different algorithm (e.g., fixed window, approximate
/// sliding window, token bucket). The strategy is selected at runtime based on the
/// <see cref="ClientRateLimit.Strategy"/> configured for the client. All strategies
/// store their state through <see cref="IRateLimitStateDatabase"/>,
/// allowing the same algorithm to work across different persistence backends.
/// </para>
/// <para>
/// Two evaluation modes are provided: <see cref="EvaluateAsync"/> increments the counter
/// (used on the live access-check path) while <see cref="PeekAsync"/> reads without
/// incrementing (used for dashboard views and accessibility reports).
/// </para>
/// </summary>
public interface IRateLimitStrategy
{
    /// <summary>
    /// Evaluates the rate limit for the given key, incrementing the counter.
    /// Used on the live request path where each call consumes a unit of quota.
    /// </summary>
    /// <param name="key">The rate limit counter key.</param>
    /// <param name="rateLimit">The rate limit configuration to evaluate against.</param>
    /// <param name="cancellationToken">Cancels the evaluation and any backing-store I/O.</param>
    /// <returns>The result of the rate limit evaluation.</returns>
    Task<RateLimitResult> EvaluateAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates the rate limit for the given key without incrementing the counter.
    /// Used for read-only views where the caller needs remaining quota information
    /// without affecting the actual count.
    /// </summary>
    /// <param name="key">The rate limit counter key.</param>
    /// <param name="rateLimit">The rate limit configuration to evaluate against.</param>
    /// <param name="cancellationToken">Cancels the peek and any backing-store I/O.</param>
    /// <returns>The result of the rate limit evaluation.</returns>
    Task<RateLimitResult> PeekAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default);
}
