namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Configuration options for historical usage tracking.
/// <para>
/// Controls two background loops inside the persistence service:
/// <list type="bullet">
///   <item><b>Fast loop</b> — flushes the in-memory buffer to per-second snapshots at
///     <see cref="SecondFlushInterval"/>. Per-second data is retained for
///     <see cref="SecondRetention"/> before being pruned.</item>
///   <item><b>Slow loop</b> — rolls up fine-grained buckets into coarser granularities
///     (5-minute, hourly, daily) at <see cref="FlushInterval"/> and prunes data that
///     has exceeded its retention window.</item>
/// </list>
/// Shorter retention windows reduce storage overhead; longer windows provide more history
/// for dashboards and exports.
/// </para>
/// </summary>
public class UsageTrackingOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "UsageTracking";

    /// <summary>
    /// How often the in-memory buffer is flushed to per-second storage. Default: 1 second.
    /// </summary>
    public TimeSpan SecondFlushInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long to retain per-second granularity buckets. Default: 3 minutes.
    /// </summary>
    public TimeSpan SecondRetention { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// How often the in-memory buffer is flushed to persistent storage. Default: 5 minutes.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long to retain 5-minute granularity buckets. Default: 24 hours.
    /// </summary>
    public TimeSpan FiveMinuteRetention { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// How long to retain hourly granularity buckets. Default: 7 days.
    /// </summary>
    public TimeSpan HourlyRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// How long to retain daily granularity buckets. Default: 90 days.
    /// </summary>
    public TimeSpan DailyRetention { get; set; } = TimeSpan.FromDays(90);
}
