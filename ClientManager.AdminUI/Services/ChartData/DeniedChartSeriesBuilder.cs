using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class DeniedChartSeriesBuilder
{
    internal static void AppendTripletSeries(
        ICollection<ClientAreaSeries> series,
        string targetId,
        string targetName,
        IReadOnlyList<HistoricalUsagePoint> points,
        DeniedViewMode mode,
        DateTime from,
        DateTime now,
        int bucketCount)
    {
        foreach (var (suffix, label) in GetTripletDefinitions(mode))
        {
            var deniedAgg = ChartBucketAggregator.Aggregate(
                points.Select(point => new ChartBucketAggregator.RawPoint(
                    point.Timestamp,
                    DeniedBreakdownHelper.GetDeniedCategoryValue(point, suffix))),
                from,
                now,
                bucketCount,
                ChartBucketAggregator.AggregationMode.Sum);

            var deniedChartPoints = deniedAgg.Buckets
                .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
                .ToList();

            series.Add(new ClientAreaSeries(
                targetId + suffix,
                $"{targetName} ({label})",
                deniedChartPoints,
                Hidden: true));
        }
    }

    private static IEnumerable<(string Suffix, string Label)> GetTripletDefinitions(DeniedViewMode mode) =>
        mode == DeniedViewMode.CapacityDenied
            ?
            [
                (ChartAggregator.DeniedUnauthSuffix, "Denied: Unauthenticated"),
                (ChartAggregator.DeniedBlockedSuffix, "Denied: Blocked"),
                (ChartAggregator.DeniedCapacitySuffix, "Denied: Capacity")
            ]
            :
            [
                (ChartAggregator.DeniedUnauthSuffix, "Denied: Unauthenticated"),
                (ChartAggregator.DeniedBlockedSuffix, "Denied: Blocked"),
                (ChartAggregator.DeniedRateLimitedSuffix, "Denied: Rate limited")
            ];
}
