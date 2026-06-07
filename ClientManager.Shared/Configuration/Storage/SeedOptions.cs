using ClientManager.Shared.Models.Entities;

namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Defines initial entities to populate when the API host starts.
/// </summary>
public class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// Client configurations to create if they do not already exist.
    /// </summary>
    public List<ClientConfiguration> ClientConfigurations { get; set; } = [];

    /// <summary>
    /// Service definitions to create if they do not already exist.
    /// </summary>
    public List<Service> Services { get; set; } = [];

    /// <summary>
    /// Resource pool definitions to create if they do not already exist.
    /// </summary>
    public List<ResourcePool> ResourcePools { get; set; } = [];

    /// <summary>
    /// Global rate-limit definitions to create if they do not already exist.
    /// </summary>
    public List<GlobalRateLimit> GlobalRateLimits { get; set; } = [];
}
