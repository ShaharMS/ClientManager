namespace ClientManager.Shared.Models.Entities;

using System.Net;

/// <summary>
/// Root configuration document for a single client.
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
    /// Whether this client is currently active.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Whether this client's requests count toward shared global rate-limit counters.
    /// </summary>
    public bool ContributesToGlobalLimits { get; init; } = true;

    /// <summary>
    /// Whether this client is exempt from being denied by global rate limits.
    /// </summary>
    public bool ExemptFromGlobalLimits { get; init; } = false;

    /// <summary>
    /// Optional per-client rate limit applied across all service requests from this client.
    /// </summary>
    public RateLimitPolicy? GlobalRateLimit { get; init; }

    /// <summary>
    /// Per-service access settings, keyed by service ID.
    /// </summary>
    public Dictionary<string, ServiceAccessSettings> Services { get; init; } = [];

    /// <summary>
    /// UTC timestamp when this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
