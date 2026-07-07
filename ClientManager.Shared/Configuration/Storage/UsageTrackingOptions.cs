namespace ClientManager.Shared.Configuration.Storage;

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
    /// Retention window for one-minute buckets.
    /// </summary>
    public TimeSpan OneMinuteRetention { get; set; } = TimeSpan.FromHours(1);

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
