namespace ClientManager.Shared.Models.Entities;

using System.Net;

/// <summary>
/// Per-service access settings for a single client, nested inside
/// <see cref="ClientConfiguration.Services"/> (keyed by service ID).
///
/// <para>
///     This record controls both the <em>static</em> access decision (is the client allowed
///     at all?) and the <em>dynamic</em> override knobs for rate-limit behavior on this
///     specific service. Any nullable property left as <c>null</c> inherits the client-wide
///     default from <see cref="ClientConfiguration"/>.
/// </para>
/// </summary>
public record ServiceAccessSettings
{
    /// <summary>
    /// Whether the client is allowed to access this service.
    /// <para>
    ///     If no <see cref="ServiceAccessSettings"/> entry exists at all for this
    ///     client–service pair, the access check returns
    ///     <see cref="HttpStatusCode.Unauthorized"/> (no relationship configured).
    ///     If an entry exists but <see cref="IsAllowed"/> is <c>false</c>, the check
    ///     returns <see cref="HttpStatusCode.Forbidden"/> (explicitly blocked).
    /// </para>
    /// </summary>
    public bool IsAllowed { get; init; } = true;

    /// <summary>
    /// Whether this client's requests to <em>this specific service</em> count toward the
    /// service's <see cref="GlobalRateLimit"/> counter.
    ///
    /// <para>
    ///     When <c>null</c>, inherits from
    ///     <see cref="ClientConfiguration.ContributesToGlobalLimits"/>.
    ///     Set explicitly to decouple this service from the client-wide default - for example,
    ///     to exclude a health-check service from contributing while the client normally
    ///     contributes everywhere else.
    /// </para>
    /// </summary>
    public bool? ContributesToGlobalLimit { get; init; }

    /// <summary>
    /// Whether this client is exempt from being denied by the service's
    /// <see cref="GlobalRateLimit"/>.
    ///
    /// <para>
    ///     When <c>null</c>, inherits from
    ///     <see cref="ClientConfiguration.ExemptFromGlobalLimits"/>.
    ///     Set explicitly to grant priority access to a critical service without changing the
    ///     client's global exemption posture.
    /// </para>
    /// </summary>
    public bool? ExemptFromGlobalLimit { get; init; }

    /// <summary>
    /// Optional per-client rate limit scoped to this service only.
    ///
    /// <para>
    ///     When set, this limit is evaluated <em>in addition to</em> the client's
    ///     <see cref="ClientConfiguration.GlobalRateLimit"/> - both must have remaining
    ///     capacity for the request to be granted. This allows fine-grained throttling
    ///     (e.g. a client may be allowed 1,000 req/min globally but only 100 req/min to
    ///     an expensive reporting service).
    /// </para>
    /// </summary>
    public ClientRateLimit? RateLimit { get; init; }
}
