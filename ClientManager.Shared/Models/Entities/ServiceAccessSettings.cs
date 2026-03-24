namespace ClientManager.Shared.Models.Entities;

using System.Net;

/// <summary>
/// Per-service access settings nested inside a <see cref="ClientConfiguration"/>.
/// The dictionary key in <see cref="ClientConfiguration.Services"/> is the service ID.
/// </summary>
public record ServiceAccessSettings
{
    /// <summary>
    /// Whether the client is allowed to access this service.
    /// <para>
    ///     The difference between this being set to <c>false</c> and the section not existing at all,
    ///     is the returned status code: 
    ///     <see cref="HttpStatusCode.Unauthorized"/> if there is no relationship between the client and service, 
    ///     <see cref="HttpStatusCode.Forbidden"/> if the client has been explicitly disabled.
    /// </para>
    /// </summary>
    public bool IsAllowed { get; init; } = true;

    /// <summary>
    /// Whether this client's requests to this service count toward the global rate-limit counter for this service.
    /// if <c>null</c>, inherits from <see cref="ClientConfiguration.ContributesToGlobalLimits"/>.
    /// </summary>
    public bool? ContributesToGlobalLimit { get; init; }

    /// <summary>
    /// Whether this client is exempt from the global rate limit for this service.
    /// if <c>null</c>, inherits from <see cref="ClientConfiguration.ExemptFromGlobalLimits"/>.
    /// </summary>
    public bool? ExemptFromGlobalLimit { get; init; }

    /// <summary>
    /// Optional per-client-per-service rate limit.
    /// </summary>
    public ClientRateLimit? RateLimit { get; init; }
}
