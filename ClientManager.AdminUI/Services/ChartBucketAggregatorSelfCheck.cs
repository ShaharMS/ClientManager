namespace ClientManager.AdminUI.Services;

/// <summary>ponytail: one runnable check that overlap + latest flat-fill behave.</summary>
internal static class ChartBucketAggregatorSelfCheck
{
    internal static void Run()
    {
        var from = new DateTime(2026, 1, 1, 10, 2, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 1, 10, 4, 0, DateTimeKind.Utc);
        var points = new[]
        {
            new ChartBucketAggregator.RawPoint(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), 42)
        };

        var latest = ChartBucketAggregator.Aggregate(
            points, from, to, 5, ChartBucketAggregator.AggregationMode.Latest, TimeSpan.FromMinutes(5));

        if (latest.Buckets.Count == 0 || latest.Buckets.Any(bucket => Math.Abs(bucket.Value - 42) > 0.001))
        {
            throw new InvalidOperationException("Latest overlap fill failed.");
        }

        var sum = ChartBucketAggregator.Aggregate(
            points, from, to, 5, ChartBucketAggregator.AggregationMode.Sum, TimeSpan.FromMinutes(5));

        if (sum.Buckets.Sum(bucket => bucket.Value) <= 0)
        {
            throw new InvalidOperationException("Sum overlap fill failed.");
        }
    }
}
