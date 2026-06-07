namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Result of evaluating a rate limit.
/// </summary>
public record RateLimitResult
{
    /// <summary>
    /// Whether the request is allowed under the evaluated policy.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Remaining requests or tokens before the limit is exhausted.
    /// </summary>
    public int RemainingRequests { get; init; }

    /// <summary>
    /// Seconds until the caller may retry when the request is denied.
    /// </summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>
    /// Whether the denial was caused by a global aggregate limit.
    /// </summary>
    public bool IsGlobalLimitHit { get; init; }
}
