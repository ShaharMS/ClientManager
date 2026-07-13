namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// A system-wide rate limit shared across all clients for one service.
/// <see cref="Id"/> is the service identifier.
///
/// <para><strong>How it differs from per-client limits</strong></para>
/// <para>
///     A <see cref="RateLimitPolicy"/> on <see cref="ClientConfiguration.GlobalRateLimit"/> or
///     <see cref="ServiceAccessSettings.RateLimit"/> throttles one individual client.
///     A <see cref="GlobalRateLimit"/> throttles the <em>aggregate</em> traffic of every client
///     hitting the same service. Both can exist simultaneously — per-client limits prevent any
///     single client from being too noisy, while the global limit protects the service from
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
/// </summary>
public record GlobalRateLimit
{
    /// <summary>
    /// Service identifier. Also the document identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Aggregate rate-limit policy applied across all contributing clients for this service.
    /// Maximum request counts apply to the <em>total</em> across all clients, not per-client.
    /// </summary>
    public RateLimitPolicy Policy { get; init; } = new();

    /// <summary>
    /// UTC timestamp when this global rate limit was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
