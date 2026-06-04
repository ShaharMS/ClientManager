using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Storage.Models.Configuration;

/// <summary>
/// Defines initial entities to populate when the storage-facing host starts.
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

/// <summary>
/// Configuration for usage-buffer flush cadence and retention windows.
/// </summary>
public class UsageTrackingOptions
{
    public const string SectionName = "UsageTracking";

    /// <summary>
    /// How often the slower rollup and pruning loop runs.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often the fast per-second flush loop runs.
    /// </summary>
    public TimeSpan SecondFlushInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Retention window for per-second buckets.
    /// </summary>
    public TimeSpan SecondRetention { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Retention window for five-minute buckets.
    /// </summary>
    public TimeSpan FiveMinuteRetention { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Retention window for hourly buckets.
    /// </summary>
    public TimeSpan HourlyRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Retention window for daily buckets.
    /// </summary>
    public TimeSpan DailyRetention { get; set; } = TimeSpan.FromDays(90);
}