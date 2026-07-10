using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Utils;

namespace ClientManager.Api.Services.Storage;

internal readonly record struct BucketTotals(
    long Granted,
    long DeniedUnauthenticated,
    long DeniedBlocked,
    long DeniedRateLimited,
    long DeniedCapacityLimited,
    long Released,
    long Active)
{
    public static BucketTotals FromUsageBucket(UsageBucket bucket) => new(
        bucket.GrantedCount,
        bucket.DeniedUnauthenticatedCount,
        bucket.DeniedBlockedCount,
        bucket.DeniedRateLimitedCount,
        bucket.DeniedCapacityLimitedCount,
        bucket.ReleasedCount,
        bucket.ActiveCount);

    public BucketTotals Add(BucketTotals other) => new(
        Granted + other.Granted,
        DeniedUnauthenticated + other.DeniedUnauthenticated,
        DeniedBlocked + other.DeniedBlocked,
        DeniedRateLimited + other.DeniedRateLimited,
        DeniedCapacityLimited + other.DeniedCapacityLimited,
        Released + other.Released,
        Active + other.Active);

    public BucketTotals WithLatestActive(long active) => this with { Active = active };
}

/// <summary>
/// Merges storage buckets into display buckets for chart responses.
/// </summary>
internal static class StatisticsBucketMerger
{
    public static IReadOnlyList<(string Label, DateTime Start, DateTime End, BucketTotals Totals)> Merge(
        IEnumerable<(DateTime Timestamp, BucketTotals Totals)> sourceBuckets,
        DateTime fromUtc,
        DateTime toUtc,
        int bucketCount,
        BucketGranularity sourceGranularity,
        bool useLatestForActive)
    {
        var bucketDuration = toUtc > fromUtc
            ? TimeSpan.FromTicks((toUtc - fromUtc).Ticks / bucketCount)
            : TimeSpan.Zero;

        if (bucketDuration <= TimeSpan.Zero || bucketCount <= 0)
        {
            return [];
        }

        var displayBuckets = new List<(string Label, DateTime Start, DateTime End, BucketTotals Totals)>(bucketCount);
        for (var i = 0; i < bucketCount; i++)
        {
            var start = fromUtc.Add(TimeSpan.FromTicks(bucketDuration.Ticks * i));
            var end = i == bucketCount - 1 ? toUtc : start.Add(bucketDuration);
            displayBuckets.Add((FormatLabel(start, toUtc - fromUtc), start, end, new BucketTotals(0, 0, 0, 0, 0, 0, 0)));
        }

        var storageDuration = BucketGranularityHelper.GetBucketDuration(sourceGranularity);
        var ordered = sourceBuckets.OrderBy(bucket => bucket.Timestamp).ToList();

        foreach (var (timestamp, totals) in ordered)
        {
            for (var i = 0; i < displayBuckets.Count; i++)
            {
                var (label, start, end, existing) = displayBuckets[i];
                if (!Overlaps(timestamp, storageDuration, start, end))
                {
                    continue;
                }

                var merged = existing.Add(totals);
                if (useLatestForActive)
                {
                    merged = merged.WithLatestActive(totals.Active);
                }

                displayBuckets[i] = (label, start, end, merged);
            }
        }

        return displayBuckets;
    }

    private static bool Overlaps(DateTime timestamp, TimeSpan storageDuration, DateTime windowStart, DateTime windowEnd)
    {
        if (storageDuration <= TimeSpan.Zero)
        {
            return timestamp >= windowStart && timestamp < windowEnd;
        }

        return BucketGranularityHelper.OverlapsRange(timestamp, storageDuration, windowStart, windowEnd);
    }

    private static string FormatLabel(DateTime start, TimeSpan totalDuration)
    {
        if (totalDuration <= TimeSpan.FromHours(1))
        {
            return start.ToString("HH:mm");
        }

        if (totalDuration <= TimeSpan.FromDays(1))
        {
            return start.ToString("HH:mm");
        }

        return start.ToString("MMM d");
    }
}
