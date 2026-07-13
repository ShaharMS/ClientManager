using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Rate-limit configuration reused in three scopes:
/// <list type="bullet">
///     <item>
///         <description>
///             <strong>Client-wide</strong> via <see cref="ClientConfiguration.GlobalRateLimit"/>:
///             caps the client's total request rate across all services.
///         </description>
///     </item>
///     <item>
///         <description>
///             <strong>Per-service</strong> via <see cref="ServiceAccessSettings.RateLimit"/>:
///             caps the client's request rate to one specific service. When both are present,
///             both are evaluated — the most restrictive one wins.
///         </description>
///     </item>
///     <item>
///         <description>
///             <strong>Global</strong> via <see cref="GlobalRateLimit.Policy"/>:
///             caps aggregate throughput for one service across all contributing clients.
///         </description>
///     </item>
/// </list>
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
