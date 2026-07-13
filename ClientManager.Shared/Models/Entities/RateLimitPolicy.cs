using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Shared rate-limit policy fields used by per-client limits and global service limits.
/// </summary>
public record RateLimitPolicy
{
    /// <summary>
    /// The rate limiting algorithm to use.
    /// </summary>
    public RateLimitStrategy Strategy { get; init; }

    /// <summary>
    /// Maximum requests in the window, or bucket capacity for <see cref="RateLimitStrategy.TokenBucket"/>.
    /// </summary>
    public int MaxRequests { get; init; }

    /// <summary>
    /// Window duration, or refill interval for <see cref="RateLimitStrategy.TokenBucket"/>.
    /// </summary>
    public TimeSpan Window { get; init; }

    /// <summary>
    /// Tokens added per refill. Only used with <see cref="RateLimitStrategy.TokenBucket"/>.
    /// </summary>
    public int? TokensPerRefill { get; init; }
}
