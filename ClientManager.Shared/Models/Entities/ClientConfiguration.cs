namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Root configuration document for a single client. Contains all per-client settings
/// including service access rules, rate limits, and resource pool quotas.
/// </summary>
public record ClientConfiguration
{
    /// <summary>
    /// Unique identifier for the client.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether this client is currently active. Disabled clients are rejected immediately.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Whether this client's requests count toward global rate-limit counters.
    /// <br></br>
    /// Defaults to <c>true</c>. Can be overridden per service.
    /// </summary>
    public bool ContributesToGlobalLimits { get; init; } = true;

    /// <summary>
    /// Whether this client is exempt from being denied by global rate limits.
    /// <br></br>
    /// Defaults to <c>false</c>. Can be overridden per service.
    /// </summary>
    public bool ExemptFromGlobalLimits { get; init; } = false;

    /// <summary>
    /// Optional per-client rate limit applied across all services.
    /// <br></br>
    /// Useful for overall throttling of noisy/spammy clients without disabling them entirely.
    /// </summary>
    public ClientRateLimit? GlobalRateLimit { get; init; }

    /// <summary>
    /// Per-service access settings, keyed by service ID.
    /// </summary>
    public Dictionary<string, ServiceAccessSettings> Services { get; init; } = new();

    /// <summary>
    /// Per-resource-pool quota settings, keyed by resource pool ID.
    /// </summary>
    public Dictionary<string, ResourcePoolSettings> ResourcePools { get; init; } = new();

    /// <summary>
    /// UTC timestamp when this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
