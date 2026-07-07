using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Utils;

/// <summary>
/// Shared bucket width, overlap, and ordering helpers for usage statistics.
/// </summary>
public static class BucketGranularityHelper
{
    public static TimeSpan GetBucketDuration(BucketGranularity granularity) => granularity switch
    {
        BucketGranularity.Second => TimeSpan.FromSeconds(1),
        BucketGranularity.OneMinute => TimeSpan.FromMinutes(1),
        BucketGranularity.FiveMinute => TimeSpan.FromMinutes(5),
        BucketGranularity.Hour => TimeSpan.FromHours(1),
        BucketGranularity.Day => TimeSpan.FromDays(1),
        _ => TimeSpan.FromMinutes(5)
    };

    public static bool OverlapsRange(DateTime bucketStart, TimeSpan bucketDuration, DateTime from, DateTime to) =>
        bucketStart < to && bucketStart.Add(bucketDuration) > from;

    public static int GetRank(BucketGranularity granularity) => granularity switch
    {
        BucketGranularity.Second => 0,
        BucketGranularity.OneMinute => 1,
        BucketGranularity.FiveMinute => 2,
        BucketGranularity.Hour => 3,
        BucketGranularity.Day => 4,
        _ => int.MaxValue
    };

    public static DateTime RoundDown(BucketGranularity granularity, DateTime utc) => granularity switch
    {
        BucketGranularity.Second => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc),
        BucketGranularity.OneMinute => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc),
        BucketGranularity.FiveMinute => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 5 * 5, 0, DateTimeKind.Utc),
        BucketGranularity.Hour => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc),
        BucketGranularity.Day => new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc),
        _ => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 5 * 5, 0, DateTimeKind.Utc)
    };

    public static BucketGranularity? TryParse(string? value) =>
        Enum.TryParse<BucketGranularity>(value, ignoreCase: true, out var parsed) ? parsed : null;
}
