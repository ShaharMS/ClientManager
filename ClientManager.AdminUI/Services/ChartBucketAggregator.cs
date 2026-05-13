namespace ClientManager.AdminUI.Services;

/// <summary>
/// Aggregates time-series data into a fixed number of buckets for chart display.
/// This ensures consistent chart density regardless of the underlying data granularity.
/// </summary>
public static class ChartBucketAggregator
{
    public const int DefaultBucketCount = 12;

    public enum AggregationMode
    {
        Sum,
        Latest
    }

    /// <summary>
    /// A raw time-series point with a UTC timestamp.
    /// </summary>
    public record RawPoint(DateTime TimestampUtc, double Value);

    /// <summary>
    /// An aggregated bucket with a display label and summed value.
    /// </summary>
    public record AggregatedBucket(string Label, double Value, DateTime BucketStart, DateTime BucketEnd);

    /// <summary>
    /// Result of aggregation including the actual bucket duration for cap scaling.
    /// </summary>
    public record AggregationResult(
        List<AggregatedBucket> Buckets,
        TimeSpan BucketDuration);

    /// <summary>
    /// Aggregates raw time-series points into a fixed number of buckets.
    /// Points are summed within each bucket. Empty buckets are filled with zero.
    /// </summary>
    /// <param name="points">Raw time-series points (UTC timestamps).</param>
    /// <param name="from">Start of the time range (UTC).</param>
    /// <param name="to">End of the time range (UTC).</param>
    /// <param name="bucketCount">Number of buckets to create (default: 12).</param>
    /// <returns>Aggregation result with buckets and bucket duration.</returns>
    public static AggregationResult Aggregate(
        IEnumerable<RawPoint> points,
        DateTime from,
        DateTime to,
        int bucketCount = DefaultBucketCount,
        AggregationMode mode = AggregationMode.Sum)
    {
        var totalDuration = to - from;
        if (totalDuration <= TimeSpan.Zero || bucketCount <= 0)
        {
            return new AggregationResult([], TimeSpan.Zero);
        }

        var bucketDuration = TimeSpan.FromTicks(totalDuration.Ticks / bucketCount);
        var buckets = new List<AggregatedBucket>();

        // Initialize buckets
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

        var latestPointsByBucket = mode == AggregationMode.Latest
            ? new Dictionary<int, RawPoint>()
            : null;

        // Aggregate points into buckets
        foreach (var point in points)
        {
            if (point.TimestampUtc < from || point.TimestampUtc > to)
                continue;

            var bucketIndex = (int)((point.TimestampUtc - from).Ticks / bucketDuration.Ticks);
            if (bucketIndex >= bucketCount)
                bucketIndex = bucketCount - 1;
            if (bucketIndex < 0)
                bucketIndex = 0;
            if (mode == AggregationMode.Sum)
            {
                var existing = buckets[bucketIndex];
                buckets[bucketIndex] = existing with { Value = existing.Value + point.Value };
                continue;
            }

            if (!latestPointsByBucket!.TryGetValue(bucketIndex, out var existingLatest)
                || point.TimestampUtc >= existingLatest.TimestampUtc)
            {
                latestPointsByBucket[bucketIndex] = point;
            }
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

    /// <summary>
    /// Aggregates multiple series of raw points into buckets, maintaining series identity.
    /// </summary>
    public static Dictionary<string, AggregationResult> AggregateMultipleSeries(
        Dictionary<string, IEnumerable<RawPoint>> seriesPoints,
        DateTime from,
        DateTime to,
        int bucketCount = DefaultBucketCount,
        AggregationMode mode = AggregationMode.Sum)
    {
        var results = new Dictionary<string, AggregationResult>();
        foreach (var (seriesId, points) in seriesPoints)
        {
            results[seriesId] = Aggregate(points, from, to, bucketCount, mode);
        }
        return results;
    }

    private static string FormatBucketLabel(DateTime start, TimeSpan totalRange)
    {
        var local = start.ToLocalTime();

        // Choose format based on total time range
        if (totalRange <= TimeSpan.FromMinutes(5))
        {
            // Very short range: show seconds
            return local.ToString("HH:mm:ss");
        }
        else if (totalRange <= TimeSpan.FromHours(6))
        {
            // Short range: show time
            return local.ToString("HH:mm");
        }
        else if (totalRange <= TimeSpan.FromDays(2))
        {
            // Medium range: show day and time
            return local.ToString("MMM dd HH:mm");
        }
        else
        {
            // Long range: show date
            return local.ToString("MMM dd");
        }
    }
}
