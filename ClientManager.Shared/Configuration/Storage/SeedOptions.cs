using ClientManager.Shared.Models.Entities;

namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Seed export/import payload and API gate configuration.
/// </summary>
public class SeedOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Seed";

    /// <summary>
    /// When <c>false</c>, seed endpoints return HTTP 404.
    /// </summary>
    public bool SeedApiEnabled { get; set; }

    /// <summary>
    /// Client configurations included in export/import payloads.
    /// </summary>
    public List<ClientConfiguration> ClientConfigurations { get; set; } = [];

    /// <summary>
    /// Service definitions included in export/import payloads.
    /// </summary>
    public List<Service> Services { get; set; } = [];

    /// <summary>
    /// Global rate-limit definitions included in export/import payloads.
    /// </summary>
    public List<GlobalRateLimit> GlobalRateLimits { get; set; } = [];
}
