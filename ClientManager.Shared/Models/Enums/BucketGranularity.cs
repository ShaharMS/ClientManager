namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Defines the time granularity for usage tracking buckets.
/// <para>
///     Each bucket represents a fixed time interval (e.g., 1 second, 5 minutes, 1 hour, 1 day) during which usage is aggregated
///     for summary and tracking purposes. The choice of granularity affects the precision and performance of usage tracking.
/// </para>
/// <para>
///     Usage tracking of more accurate granularity (e.g., per second) allows for more precise rate limiting and usage analysis, but require more storage and processing overhead,
///     and such more and more accurate periods are available for shorter time windows (for example, per-five-minutes buckets, by default, are only kept for the last hour.)
/// </para>
/// </summary>
public enum BucketGranularity
{
    /// <summary>
    /// Usage is aggregated into 1-second buckets. This is the most precise granularity, allowing for detailed usage tracking.
    /// <br></br>
    /// Resolution is the highest, is persisted for the least amount of time.
    /// </summary>
    Second,
    /// <summary>
    /// Usage is aggregated into 5-minute buckets. This provides a balance between precision and performance, suitable for short-term tracking needs.
    /// <br></br>
    /// Resolution is okay, is persisted for a medium amount of time.
    /// </summary>
    FiveMinute,
    /// <summary>
    /// Usage is aggregated into 1-hour buckets. This is a coarser granularity, suitable for medium-to-long-term usage tracking and analysis.
    /// <br></br>
    /// Resolution is low, is persisted for a long amount of time.
    /// </summary>
    Hour,
    /// <summary>
    /// Usage is aggregated into 1-day buckets. This is the coarsest granularity, suitable for mostly for trend analysis.
    /// <br></br>
    /// Resolution is the lowest, is persisted for the longest amount of time.
    /// </summary>
    Day
}
