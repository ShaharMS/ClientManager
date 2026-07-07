using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class TargetBreakdownChartSeriesBuilder
{
    internal static (List<ClientAreaSeries> Series, List<ChartBucketAggregator.AggregatedBucket> ReferenceBuckets) Build(
        IEnumerable<(string Id, string Name, IReadOnlyList<HistoricalUsagePoint> Points)> targets,
        bool usageIsSummed,
        DeniedViewMode deniedViewMode,
        DateTime from,
        DateTime now,
        int bucketCount,
        IStringLocalizer<SharedResources> localizer,
        bool showDenied = false,
        TimeSpan? storageBucketDuration = null)
    {
        var usageMode = ChartValueHelper.GetAggregationMode(usageIsSummed);
        var storageDuration = storageBucketDuration ?? TimeSpan.Zero;
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
                usageMode,
                storageDuration);
            referenceBuckets ??= usageAgg.Buckets;

            var usageChartPoints = usageAgg.Buckets
                .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
                .ToList();
            if (usageChartPoints.Any(point => point.Value > 0))
            {
                series.Add(new ClientAreaSeries(id, name, usageChartPoints));
            }

            DeniedChartSeriesBuilder.AppendTripletSeries(
                series, id, points, deniedViewMode, from, now, bucketCount, localizer, showDenied, storageDuration);
        }

        referenceBuckets ??= ChartBucketAggregator.Aggregate([], from, now, bucketCount).Buckets;
        return (series, referenceBuckets);
    }
}
