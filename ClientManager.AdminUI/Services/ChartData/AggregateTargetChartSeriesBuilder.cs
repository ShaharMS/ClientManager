using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class AggregateTargetChartSeriesBuilder
{
    internal static (List<ClientAreaSeries> Series, List<ChartBucketAggregator.AggregatedBucket> ReferenceBuckets) Build(
        IEnumerable<IReadOnlyList<HistoricalUsagePoint>> targetPoints,
        bool usageIsSummed,
        string seriesLabel,
        DeniedViewMode deniedViewMode,
        DateTime from,
        DateTime now,
        int bucketCount)
    {
        var targetPointLists = targetPoints.ToList();
        var usageMode = ChartValueHelper.GetAggregationMode(usageIsSummed);
        var aggregations = targetPointLists
            .Select(points => ChartBucketAggregator.Aggregate(
                points.Select(point => new ChartBucketAggregator.RawPoint(
                    point.Timestamp,
                    ChartValueHelper.GetHistoricalPointValue(point, usageIsSummed))),
                from,
                now,
                bucketCount,
                usageMode))
            .Where(result => result.Buckets.Count > 0)
            .ToList();

        var referenceBuckets = aggregations.FirstOrDefault()?.Buckets
            ?? ChartBucketAggregator.Aggregate([], from, now, bucketCount, usageMode).Buckets;

        var chartPoints = ChartValueHelper.SumBuckets(aggregations, referenceBuckets);

        var series = new List<ClientAreaSeries>();
        if (chartPoints.Count > 0)
        {
            series.Add(new ClientAreaSeries(ChartAggregator.AggregateSeriesId, seriesLabel, chartPoints));
        }

        var mergedHistory = HistoricalPointMerger.SumByTimestamp(targetPointLists);
        DeniedChartSeriesBuilder.AppendTripletSeries(
            series,
            ChartAggregator.AggregateSeriesId,
            mergedHistory,
            deniedViewMode,
            from,
            now,
            bucketCount);

        return (series, referenceBuckets);
    }
}
