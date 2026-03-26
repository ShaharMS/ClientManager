namespace ClientManager.Api.Models.Entities;

/// <summary>
/// The result of a rate limit evaluation.
/// </summary>
public record RateLimitResult
{
    /// <summary>
    /// Whether the request is allowed under the evaluated rate limit.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// The number of remaining requests before the limit is reached.
    /// </summary>
    public int RemainingRequests { get; init; }

    /// <summary>
    /// Seconds until the client may retry, if rate limited.
    /// <c>null</c> when the request was allowed.
    /// </summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>
    /// Whether the denial was caused by a global aggregate limit rather than a per-client limit.
    /// Used to distinguish denial reasons in metrics and error responses.
    /// </summary>
    public bool IsGlobalLimitHit { get; init; }
}
