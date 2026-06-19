using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Static helpers for splitting <see cref="Shared.Models.Entities.UsageSnapshot"/> documents/// into bounded time segments.
///
/// <para><strong>Problem</strong></para>
/// <para>
///     Without segments, there is one snapshot document per (client, target, granularity).
///     That document grows without bound as new buckets are appended every flush cycle.
///     With per-second granularity and 3-minute retention that's manageable (~180 buckets),
///     but increasing retention to hours or days would make the document too large to
///     efficiently read, modify, and write back on every cycle.
/// </para>
///
/// <para><strong>Solution</strong></para>
/// <para>
///     Each snapshot is split into segments — separate documents that each cover a fixed
///     time window. The window size is chosen so the maximum bucket count per document stays
///     reasonable:
///     <see cref="BucketGranularity.Second"/> → 1-hour segments (≤3600 buckets),
///     <see cref="BucketGranularity.FiveMinute"/> → 1-day segments (≤288 buckets),
///     <see cref="BucketGranularity.Hour"/> → 1-week segments (≤168 buckets),
///     <see cref="BucketGranularity.Day"/> → 1-month segments (≤31 buckets).
/// </para>
///
/// <para><strong>Benefits</strong></para>
/// <list type="bullet">
///     <item><description>Flush writes only the current segment, not the entire history.</description></item>
///     <item><description>Prune drops whole segment documents when fully expired — no read-filter-rewrite.</description></item>
///     <item><description>Range queries fetch only the segments that overlap the requested time window.</description></item>
///     <item><description>Retention can be extended without affecting per-document size.</description></item>
/// </list>
/// </summary>
public static class UsageSegmentHelper
{
    /// <summary>
    /// Builds the compound document ID for a specific time segment.
    /// Extends the base ID pattern with a <c>yyyyMMddHH</c> suffix so each segment
    /// is a separate, bounded document.
    /// </summary>
    public static string BuildSegmentId(
        string clientId, TargetType targetType, string targetId,
        BucketGranularity granularity, DateTime segmentStart)
        => $"{clientId}:{targetType}:{targetId}:{granularity}:{segmentStart:yyyyMMddHH}";

    /// <summary>
    /// Returns the start of the segment window that contains <paramref name="timestamp"/>
    /// for the given <paramref name="granularity"/>.
    /// </summary>
    public static DateTime GetSegmentStart(DateTime timestamp, BucketGranularity granularity)
    {
        return granularity switch
        {
            // Second-granularity segments span 1 hour (max ~3600 buckets).
            BucketGranularity.Second => new DateTime(
                timestamp.Year, timestamp.Month, timestamp.Day,
                timestamp.Hour, 0, 0, DateTimeKind.Utc),

            // FiveMinute-granularity segments span 1 day (max 288 buckets).
            BucketGranularity.FiveMinute => new DateTime(
                timestamp.Year, timestamp.Month, timestamp.Day,
                0, 0, 0, DateTimeKind.Utc),

            // Hour-granularity segments span 1 week (max 168 buckets).
            // Align to the Monday of the ISO week containing the timestamp.
            BucketGranularity.Hour => GetMondayOfWeek(timestamp),

            // Day-granularity segments span 1 month (max 31 buckets).
            BucketGranularity.Day => new DateTime(
                timestamp.Year, timestamp.Month, 1,
                0, 0, 0, DateTimeKind.Utc),

            _ => new DateTime(
                timestamp.Year, timestamp.Month, timestamp.Day,
                0, 0, 0, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Enumerates the segment start timestamps that cover the half-open range
    /// [<paramref name="from"/>, <paramref name="to"/>).
    /// </summary>
    public static IEnumerable<DateTime> EnumerateSegmentStarts(
        DateTime from, DateTime to, BucketGranularity granularity)
    {
        var current = GetSegmentStart(from, granularity);

        while (current < to)
        {
            yield return current;
            current = AdvanceSegment(current, granularity);
        }
    }

    private static DateTime AdvanceSegment(DateTime segmentStart, BucketGranularity granularity)
    {
        return granularity switch
        {
            BucketGranularity.Second => segmentStart.AddHours(1),
            BucketGranularity.FiveMinute => segmentStart.AddDays(1),
            BucketGranularity.Hour => segmentStart.AddDays(7),
            BucketGranularity.Day => segmentStart.AddMonths(1),
            _ => segmentStart.AddDays(1)
        };
    }

    /// <summary>
    /// Returns the end of the segment window starting at <paramref name="segmentStart"/>.
    /// This is the exclusive upper bound — the next segment starts at this timestamp.
    /// </summary>
    public static DateTime GetSegmentEnd(DateTime segmentStart, BucketGranularity granularity)
        => AdvanceSegment(segmentStart, granularity);

    private static DateTime GetMondayOfWeek(DateTime timestamp)
    {
        var dayOfWeek = (int)timestamp.DayOfWeek;
        // DayOfWeek.Sunday == 0, Monday == 1, ..., Saturday == 6
        // We want Monday-aligned weeks, so shift Sunday to 7.
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        var monday = timestamp.Date.AddDays(-daysFromMonday);
        return DateTime.SpecifyKind(monday, DateTimeKind.Utc);
    }

    /// <summary>
    /// Builds the storage counter key for a pending usage bucket delta.
    /// </summary>
    public static string BuildUsageCounterKey(
        string clientId,
        TargetType targetType,
        string targetId,
        DateTime bucketTimestamp,
        UsageEventType eventType) =>
        $"usage:{clientId}:{targetType}:{targetId}:{BucketGranularity.Second}:{bucketTimestamp:yyyyMMddHHmmss}:{eventType}";

    /// <summary>
    /// Enumerates storage counter keys for each second and event type in the inclusive range.
    /// </summary>
    public static IEnumerable<string> EnumerateUsageCounterKeys(
        string clientId,
        TargetType targetType,
        string targetId,
        DateTime from,
        DateTime to)
    {
        var cursor = RoundDownToSecond(from);
        var end = RoundDownToSecond(to);

        while (cursor <= end)
        {
            foreach (UsageEventType eventType in Enum.GetValues<UsageEventType>())
            {
                yield return BuildUsageCounterKey(clientId, targetType, targetId, cursor, eventType);
            }

            cursor = cursor.AddSeconds(1);
        }
    }

    /// <summary>
    /// Parses a usage counter key produced by <see cref="BuildUsageCounterKey"/>.
    /// </summary>
    public static bool TryParseUsageCounterKey(
        string key,
        out string clientId,
        out TargetType targetType,
        out string targetId,
        out DateTime bucketTimestamp,
        out UsageEventType eventType)
    {
        clientId = string.Empty;
        targetId = string.Empty;
        targetType = default;
        bucketTimestamp = default;
        eventType = default;

        if (!key.StartsWith("usage:", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = key.Split(':', 7);
        if (parts.Length != 7 ||
            !Enum.TryParse(parts[2], out targetType) ||
            !Enum.TryParse(parts[6], out eventType) ||
            !DateTime.TryParseExact(
                parts[5],
                "yyyyMMddHHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out bucketTimestamp))
        {
            return false;
        }

        clientId = parts[1];
        targetId = parts[3];
        return true;
    }

    /// <summary>
    /// Rounds a UTC timestamp down to the nearest second.
    /// </summary>
    public static DateTime RoundDownToSecond(DateTime utc) =>
        new(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
}
