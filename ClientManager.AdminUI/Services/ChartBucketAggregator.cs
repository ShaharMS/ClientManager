namespace ClientManager.AdminUI.Services;

/// <summary>
/// Aggregates time-series data into a fixed number of buckets for chart display.
/// This ensures consistent chart density regardless of the underlying data granularity.
/// </summary>
public static class ChartBucketAggregator
{
    public const int MinBucketCount = 5;
    public const int MaxBucketCount = 20;
    public const int DefaultBucketCount = MaxBucketCount;
    private const int PixelsPerBucket = 80;

    /// <summary>Maps chart width to bucket count (~1 bucket per 80px, clamped 5–20).</summary>
    public static int GetBucketCountForWidth(int chartWidthPx) =>
        Math.Clamp(chartWidthPx / PixelsPerBucket, MinBucketCount, MaxBucketCount);

    public enum AggregationMode
    {
        Sum,
        Latest
    }

    public record RawPoint(DateTime TimestampUtc, double Value);

    public record AggregatedBucket(string Label, double Value, DateTime BucketStart, DateTime BucketEnd);

    public record AggregationResult(
        List<AggregatedBucket> Buckets,
        TimeSpan BucketDuration);

    public static AggregationResult Aggregate(
        IEnumerable<RawPoint> points,
        DateTime from,
        DateTime to,
        int bucketCount = MaxBucketCount,
        AggregationMode mode = AggregationMode.Sum,
        TimeSpan? storageBucketDuration = null)
    {
        var totalDuration = to - from;
        if (totalDuration <= TimeSpan.Zero || bucketCount <= 0)
        {
            return new AggregationResult([], TimeSpan.Zero);
        }

        var bucketDuration = TimeSpan.FromTicks(totalDuration.Ticks / bucketCount);
        var buckets = new List<AggregatedBucket>();

        for (var i = 0; i < bucketCount; i++)
        {
            var bucketStart = from.Add(TimeSpan.FromTicks(bucketDuration.Ticks * i));
            var bucketEnd = i == bucketCount - 1 ? to : bucketStart.Add(bucketDuration);
            buckets.Add(new AggregatedBucket(
                FormatBucketLabel(bucketStart, totalDuration),
                0,
                bucketStart,
                bucketEnd));
        }

        var pointList = points.ToList();
        var storageDuration = storageBucketDuration ?? TimeSpan.Zero;

        if (mode == AggregationMode.Latest
            && storageDuration > TimeSpan.Zero
            && totalDuration < storageDuration)
        {
            var overlapping = pointList
                .Where(point => OverlapsRange(point.TimestampUtc, storageDuration, from, to))
                .OrderByDescending(point => point.TimestampUtc)
                .FirstOrDefault();

            if (overlapping is not null)
            {
                for (var i = 0; i < bucketCount; i++)
                {
                    var existing = buckets[i];
                    buckets[i] = existing with { Value = overlapping.Value };
                }

                return new AggregationResult(buckets, bucketDuration);
            }
        }

        var latestPointsByBucket = mode == AggregationMode.Latest
            ? new Dictionary<int, RawPoint>()
            : null;

        foreach (var point in pointList)
        {
            if (storageDuration > TimeSpan.Zero)
            {
                if (!OverlapsRange(point.TimestampUtc, storageDuration, from, to))
                {
                    continue;
                }

                AssignOverlappingPoint(buckets, point, storageDuration, mode, latestPointsByBucket);
                continue;
            }

            if (point.TimestampUtc < from || point.TimestampUtc > to)
            {
                continue;
            }

            AssignPointToBucket(buckets, from, bucketDuration, bucketCount, point, mode, latestPointsByBucket);
        }

        if (mode == AggregationMode.Latest && latestPointsByBucket is not null)
        {
            foreach (var (bucketIndex, point) in latestPointsByBucket)
            {
                var existing = buckets[bucketIndex];
                buckets[bucketIndex] = existing with { Value = point.Value };
            }
        }

        return new AggregationResult(buckets, bucketDuration);
    }

    public static Dictionary<string, AggregationResult> AggregateMultipleSeries(
        Dictionary<string, IEnumerable<RawPoint>> seriesPoints,
        DateTime from,
        DateTime to,
        int bucketCount = MaxBucketCount,
        AggregationMode mode = AggregationMode.Sum,
        TimeSpan? storageBucketDuration = null)
    {
        var results = new Dictionary<string, AggregationResult>();
        foreach (var (seriesId, points) in seriesPoints)
        {
            results[seriesId] = Aggregate(points, from, to, bucketCount, mode, storageBucketDuration);
        }

        return results;
    }

    private static void AssignOverlappingPoint(
        List<AggregatedBucket> buckets,
        RawPoint point,
        TimeSpan storageDuration,
        AggregationMode mode,
        Dictionary<int, RawPoint>? latestPointsByBucket)
    {
        var pointStart = point.TimestampUtc;
        var pointEnd = point.TimestampUtc.Add(storageDuration);

        for (var i = 0; i < buckets.Count; i++)
        {
            var displayStart = buckets[i].BucketStart;
            var displayEnd = buckets[i].BucketEnd;
            if (!OverlapsRange(displayStart, displayEnd - displayStart, pointStart, pointEnd))
            {
                continue;
            }

            if (mode == AggregationMode.Sum)
            {
                var existing = buckets[i];
                buckets[i] = existing with { Value = existing.Value + point.Value };
            }
            else if (!latestPointsByBucket!.TryGetValue(i, out var existingLatest)
                     || point.TimestampUtc >= existingLatest.TimestampUtc)
            {
                latestPointsByBucket[i] = point;
            }
        }
    }

    private static void AssignPointToBucket(
        List<AggregatedBucket> buckets,
        DateTime from,
        TimeSpan bucketDuration,
        int bucketCount,
        RawPoint point,
        AggregationMode mode,
        Dictionary<int, RawPoint>? latestPointsByBucket)
    {
        var bucketIndex = (int)((point.TimestampUtc - from).Ticks / bucketDuration.Ticks);
        bucketIndex = Math.Clamp(bucketIndex, 0, bucketCount - 1);

        if (mode == AggregationMode.Sum)
        {
            var existing = buckets[bucketIndex];
            buckets[bucketIndex] = existing with { Value = existing.Value + point.Value };
            return;
        }

        if (!latestPointsByBucket!.TryGetValue(bucketIndex, out var existingLatest)
            || point.TimestampUtc >= existingLatest.TimestampUtc)
        {
            latestPointsByBucket[bucketIndex] = point;
        }
    }

    private static bool OverlapsRange(DateTime rangeStart, TimeSpan rangeDuration, DateTime from, DateTime to) =>
        rangeStart < to && rangeStart.Add(rangeDuration) > from;

    private static string FormatBucketLabel(DateTime start, TimeSpan totalRange)
    {
        var local = start.ToLocalTime();

        if (totalRange <= TimeSpan.FromMinutes(5))
        {
            return local.ToString("HH:mm:ss");
        }

        if (totalRange <= TimeSpan.FromHours(6))
        {
            return local.ToString("HH:mm");
        }

        if (totalRange <= TimeSpan.FromDays(2))
        {
            return local.ToString("MMM dd HH:mm");
        }

        return local.ToString("MMM dd");
    }
}
