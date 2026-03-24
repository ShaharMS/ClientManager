namespace ClientManager.Shared.Models.Entities;

using ClientManager.Shared.Models.Enums;
using System.Net;

/// <summary>
/// Root configuration document for a single client. Defines everything the system needs to
/// know about a client: which services it may call, how fast it may call them, and how many
/// resource-pool slots it may hold.
///
/// <para><strong>Settings hierarchy and override model</strong></para>
/// <para>
///     Several settings follow a <em>client-level default → per-target override</em> pattern.
///     The client-level value acts as the fallback; a per-target value (when non-null) wins.
/// </para>
/// <list type="bullet">
///     <item>
///         <description>
///             <see cref="ContributesToGlobalLimits"/> is the client-wide default.
///             <see cref="ServiceAccessSettings.ContributesToGlobalLimit"/> overrides it for
///             one specific service.
///         </description>
///     </item>
///     <item>
///         <description>
///             <see cref="ExemptFromGlobalLimits"/> is the client-wide default.
///             <see cref="ServiceAccessSettings.ExemptFromGlobalLimit"/> overrides it for one
///             specific service.
///         </description>
///     </item>
///     <item>
///         <description>
///             <see cref="GlobalRateLimit"/> applies across all services as a blanket throttle.
///             <see cref="ServiceAccessSettings.RateLimit"/> adds a separate, per-service
///             throttle that is evaluated <em>in addition to</em> (not instead of) the global one.
///         </description>
///     </item>
/// </list>
///
/// <para><strong>Access model</strong></para>
/// <para>
///     Access is <strong>deny-by-default</strong>. A client can only reach a service if:
///     (1) <see cref="IsEnabled"/> is <c>true</c>,
///     (2) a <see cref="ServiceAccessSettings"/> entry exists in <see cref="Services"/> for
///     that service, and
///     (3) <see cref="ServiceAccessSettings.IsAllowed"/> is <c>true</c>.
///     After those static checks pass, the request still has to clear global and per-client
///     rate limits before it is granted.
/// </para>
///
/// <para><strong>Resource pool quotas</strong></para>
/// <para>
///     Entries in <see cref="ResourcePools"/> set per-client concurrency caps on resource
///     pools. They do <em>not</em> grant or deny access on their own - they only limit how
///     many slots this specific client may hold at the same time. If no entry exists for a
///     pool, the client can still acquire slots (up to the pool's system-wide
///     <see cref="ResourcePool.MaxSlots"/>), but has no individual cap.
/// </para>
/// </summary>
public record ClientConfiguration
{
    /// <summary>
    /// Unique identifier for the client.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether this client is currently active. Disabled clients are rejected immediately
    /// with a 403 Forbidden response - no further checks (rate limits, service access, quotas)
    /// are evaluated.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Whether this client's requests count toward the shared global rate-limit counters
    /// (<see cref="GlobalRateLimit"/> entities with <see cref="TargetType.Service"/> or
    /// <see cref="TargetType.ResourcePool"/>).
    ///
    /// <para>
    ///     When <c>true</c> (the default), every granted or evaluated request from this client
    ///     increments the global counter, consuming capacity for all other clients too.
    ///     Set to <c>false</c> for internal/trusted clients whose traffic should not eat into
    ///     the shared budget.
    /// </para>
    /// <para>
    ///     Can be overridden per service via
    ///     <see cref="ServiceAccessSettings.ContributesToGlobalLimit"/>.
    /// </para>
    /// </summary>
    public bool ContributesToGlobalLimits { get; init; } = true;

    /// <summary>
    /// Whether this client is exempt from being <em>denied</em> by global rate limits.
    ///
    /// <para>
    ///     This is independent of <see cref="ContributesToGlobalLimits"/>. A client can
    ///     contribute to the counter (so its traffic is visible) yet still be exempt from
    ///     denial (so it is never blocked). Conversely, a client can be non-contributing
    ///     but still subject to global denials.
    /// </para>
    /// <para>
    ///     Defaults to <c>false</c>. Can be overridden per service via
    ///     <see cref="ServiceAccessSettings.ExemptFromGlobalLimit"/>.
    /// </para>
    /// </summary>
    public bool ExemptFromGlobalLimits { get; init; } = false;

    /// <summary>
    /// Optional per-client rate limit applied across <em>all</em> service requests from this
    /// client, regardless of which service is being called.
    ///
    /// <para>
    ///     Useful for throttling noisy or spammy clients without disabling them entirely.
    ///     When a service also has its own <see cref="ServiceAccessSettings.RateLimit"/>,
    ///     <em>both</em> limits are evaluated - the most restrictive one wins.
    /// </para>
    /// </summary>
    public ClientRateLimit? GlobalRateLimit { get; init; }

    /// <summary>
    /// Per-service access settings, keyed by service ID.
    ///
    /// <para>
    ///     If a service ID is absent from this dictionary, the client has no relationship
    ///     with that service and requests are rejected with <see cref="HttpStatusCode.Unauthorized"/> (401).
    /// </para>
    /// </summary>
    public Dictionary<string, ServiceAccessSettings> Services { get; init; } = [];

    /// <summary>
    /// Per-resource-pool quota settings, keyed by resource pool ID.
    ///
    /// <para>
    ///     Each entry caps how many concurrent slots this client may hold in the
    ///     corresponding pool. Pools not listed here have no per-client cap (the
    ///     resource pool specific <see cref="ResourcePool.MaxSlots"/> still applies).
    /// </para>
    /// </summary>
    public Dictionary<string, ResourcePoolSettings> ResourcePools { get; init; } = [];

    /// <summary>
    /// UTC timestamp when this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
