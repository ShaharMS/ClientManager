namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Per-service access settings nested inside a <see cref="ClientConfiguration"/>.
/// The dictionary key in <see cref="ClientConfiguration.Services"/> is the service ID.
/// </summary>
public record ServiceAccessSettings
{
    /// <summary>Whether the client is allowed to access this service.</summary>
    public bool IsAllowed { get; init; } = true;

    /// <summary>
    /// Whether this client's requests to this service count toward the global rate-limit counter for this service.
    /// <c>null</c> inherits from <see cref="ClientConfiguration.ContributesToGlobalLimits"/>.
    /// </summary>
    public bool? ContributesToGlobalLimit { get; init; }

    /// <summary>
    /// Whether this client is exempt from the global rate limit for this service.
    /// <c>null</c> inherits from <see cref="ClientConfiguration.ExemptFromGlobalLimits"/>.
    /// </summary>
    public bool? ExemptFromGlobalLimit { get; init; }

    /// <summary>
    /// Optional per-client-per-service rate limit.
    /// </summary>
    public ClientRateLimit? RateLimit { get; init; }
}
