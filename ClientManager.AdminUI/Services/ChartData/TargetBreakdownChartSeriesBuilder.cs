using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class TargetBreakdownChartSeriesBuilder
{
    internal static (List<ClientAreaSeries> Series, List<ChartBucketAggregator.AggregatedBucket> ReferenceBuckets) Build(
        IEnumerable<(string Id, string Name, IReadOnlyList<HistoricalUsagePoint> Points)> targets,
        bool usageIsSummed,
        DeniedViewMode deniedViewMode,
        DateTime from,
        DateTime now,
        int bucketCount)
    {
        var usageMode = ChartValueHelper.GetAggregationMode(usageIsSummed);
        var series = new List<ClientAreaSeries>();
        List<ChartBucketAggregator.AggregatedBucket>? referenceBuckets = null;

        foreach (var (id, name, points) in targets)
        {
            if (points.Count == 0)
            {
                continue;
            }

            var usageAgg = ChartBucketAggregator.Aggregate(
                points.Select(point => new ChartBucketAggregator.RawPoint(
                    point.Timestamp,
                    ChartValueHelper.GetHistoricalPointValue(point, usageIsSummed))),
                from,
                now,
                bucketCount,
                usageMode);
            referenceBuckets ??= usageAgg.Buckets;

            var usageChartPoints = usageAgg.Buckets
                .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
                .ToList();
            if (usageChartPoints.Any(point => point.Value > 0))
            {
                series.Add(new ClientAreaSeries(id, name, usageChartPoints));
            }

            DeniedChartSeriesBuilder.AppendTripletSeries(
                series, id, points, deniedViewMode, from, now, bucketCount);
        }

        referenceBuckets ??= ChartBucketAggregator.Aggregate([], from, now, bucketCount).Buckets;
        return (series, referenceBuckets);
    }
}
