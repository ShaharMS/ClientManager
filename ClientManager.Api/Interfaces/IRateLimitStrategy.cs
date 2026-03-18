using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Interfaces;

/// <summary>
/// Defines a rate limiting algorithm that evaluates whether a request should be allowed.
/// </summary>
public interface IRateLimitStrategy
{
    /// <summary>
    /// Evaluates the rate limit for the given key, incrementing the counter.
    /// </summary>
    /// <param name="key">The rate limit counter key.</param>
    /// <param name="rateLimit">The rate limit configuration to evaluate against.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the rate limit evaluation.</returns>
    Task<RateLimitResult> EvaluateAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates the rate limit for the given key without incrementing the counter.
    /// </summary>
    /// <param name="key">The rate limit counter key.</param>
    /// <param name="rateLimit">The rate limit configuration to evaluate against.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the rate limit evaluation.</returns>
    Task<RateLimitResult> PeekAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default);
}
