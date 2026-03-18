using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Models;

/// <summary>
/// Defines initial entities to populate when the application starts for the first time.
/// Bound from the <c>"Seed"</c> section of <c>appsettings.json</c>.
/// <para>
/// This is useful for bootstrapping a new deployment with predefined services, resource pools,
/// global rate limits, and client configurations so the system is usable immediately without
/// requiring manual API calls. Entities are only created if they don't already exist (by ID),
/// so re-running the application won't duplicate seed data.
/// </para>
/// </summary>
public class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// Client configurations to create on first startup.
    /// Each entry is a full <see cref="ClientConfiguration"/> document with nested service access,
    /// rate limits, and resource pool quotas.
    /// </summary>
    public List<ClientConfiguration> ClientConfigurations { get; set; } = [];

    /// <summary>
    /// Service definitions to create on first startup.
    /// Services represent the downstream APIs that clients request access to.
    /// </summary>
    public List<Service> Services { get; set; } = [];

    /// <summary>
    /// Resource pool definitions to create on first startup.
    /// Resource pools represent finite shared resources (e.g., database connections, S3 handles)
    /// that clients must explicitly acquire and release.
    /// </summary>
    public List<ResourcePool> ResourcePools { get; set; } = [];

    /// <summary>
    /// Global rate limit definitions to create on first startup.
    /// These cap aggregate traffic from all contributing clients to a specific service or resource pool.
    /// </summary>
    public List<GlobalRateLimit> GlobalRateLimits { get; set; } = [];
}
