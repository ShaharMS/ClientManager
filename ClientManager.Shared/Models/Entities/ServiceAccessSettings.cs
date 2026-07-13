namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Per-service access settings for a single client.
/// </summary>
public record ServiceAccessSettings
{
    /// <summary>
    /// Whether the client is allowed to access this service.
    /// </summary>
    public bool IsAllowed { get; init; } = true;

    /// <summary>
    /// Whether this client's requests to this service count toward the service global rate limit.
    /// </summary>
    public bool? ContributesToGlobalLimit { get; init; }

    /// <summary>
    /// Whether this client is exempt from being denied by the service global rate limit.
    /// </summary>
    public bool? ExemptFromGlobalLimit { get; init; }

    /// <summary>
    /// Optional per-client rate limit scoped to this service only.
    /// </summary>
    public RateLimitPolicy? RateLimit { get; init; }
}
