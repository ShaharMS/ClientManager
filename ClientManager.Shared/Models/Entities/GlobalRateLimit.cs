using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// A system-wide rate limit shared across all clients for a single target (service or resource pool).
///
/// <para><strong>How it differs from <see cref="ClientRateLimit"/></strong></para>
/// <para>
///     A <see cref="ClientRateLimit"/> throttles one individual client. A
///     <see cref="GlobalRateLimit"/> throttles the <em>aggregate</em> traffic of every client
///     hitting the same target. Both can exist simultaneously - per-client limits prevent any
///     single client from being too noisy, while the global limit protects the target from
///     being overwhelmed even if every client is individually well-behaved.
/// </para>
///
/// <para><strong>Contribute vs. exempt</strong></para>
/// <para>
///     Not every client interacts with the global counter in the same way. Two independent
///     flags control a client's relationship to this limit:
/// </para>
/// <list type="bullet">
///     <item>
///         <description>
///             <strong>Contributes</strong>
///             (<see cref="ClientConfiguration.ContributesToGlobalLimits"/> /
///             <see cref="ServiceAccessSettings.ContributesToGlobalLimit"/>):
///             when <c>true</c>, the client's requests increment this counter, consuming
///             shared capacity. Disable this for internal or trusted clients whose traffic
///             should not eat into the budget visible to others.
///         </description>
///     </item>
///     <item>
///         <description>
///             <strong>Exempt</strong>
///             (<see cref="ClientConfiguration.ExemptFromGlobalLimits"/> /
///             <see cref="ServiceAccessSettings.ExemptFromGlobalLimit"/>):
///             when <c>true</c>, the client is never denied by this counter, even if it is
///             exhausted. Use this for high-priority clients that must always get through.
///         </description>
///     </item>
/// </list>
/// <para>
///     These two flags are orthogonal: a client can contribute without being exempt
///     (the common case), be exempt without contributing (high-priority, invisible),
///     both, or neither.
/// </para>
///
/// <para><strong>Applicability by target type</strong></para>
/// <para>
///     For <see cref="TargetType.Service"/>: caps aggregate request throughput (e.g. "this
///     API can handle 10,000 requests/minute total").
///     For <see cref="TargetType.ResourcePool"/>: caps aggregate slot-acquisition frequency,
///     preventing thundering-herd bursts against the pool, independent of the pool's slot
///     capacity.
/// </para>
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
    /// Whether the target is a service or a resource pool. Determines the access-control
    /// path that evaluates this limit.
    /// </summary>
    public TargetType TargetType { get; init; }

    /// <summary>
    /// The rate-limiting algorithm to use.
    /// </summary>
    public RateLimitStrategy Strategy { get; init; }

    /// <summary>
    /// Maximum aggregate requests allowed in the window (or bucket capacity for
    /// <see cref="RateLimitStrategy.TokenBucket"/>). This is the total across <em>all</em>
    /// contributing clients, not per-client.
    /// </summary>
    public int MaxRequests { get; init; }

    /// <summary>
    /// Window duration (or refill interval for <see cref="RateLimitStrategy.TokenBucket"/>).
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
