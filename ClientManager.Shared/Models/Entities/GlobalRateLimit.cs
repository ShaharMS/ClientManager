using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Defines a system-wide aggregate rate limit that caps total traffic from all contributing clients
/// to a specific service or resource pool.
/// </summary>
public record GlobalRateLimit
{
    /// <summary>
    /// Unique identifier for this global rate limit.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// ID of the service or resource pool this limit applies to.
    /// </summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Whether the target is a service or a resource pool.
    /// </summary>
    public GlobalRateLimitTarget TargetType { get; init; }

    /// <summary>
    /// The rate limiting algorithm to use.
    /// </summary>
    public RateLimitStrategy Strategy { get; init; }

    /// <summary>
    /// Maximum aggregate requests in the window, or bucket capacity for token bucket.
    /// </summary>
    public int MaxRequests { get; init; }

    /// <summary>
    /// Window duration, or refill interval for token bucket.
    /// </summary>
    public TimeSpan Window { get; init; }

    /// <summary>
    /// Tokens added per refill. Only used with <see cref="RateLimitStrategy.TokenBucket"/>.
    /// </summary>
    public int? TokensPerRefill { get; init; }

    /// <summary>
    /// UTC timestamp when this global rate limit was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
