using ClientManager.AdminUI.Localization;
using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class DeniedChartSeriesBuilder
{
    internal static void AppendTripletSeries(
        ICollection<ClientAreaSeries> series,
        string targetId,
        IReadOnlyList<HistoricalUsagePoint> points,
        DeniedViewMode mode,
        DateTime from,
        DateTime now,
        int bucketCount,
        IStringLocalizer<SharedResources> localizer,
        bool showDenied = false,
        TimeSpan? storageBucketDuration = null)
    {
        if (!showDenied)
        {
            return;
        }

        foreach (var (suffix, label) in GetTripletDefinitions(mode, localizer))
        {
            var deniedAgg = ChartBucketAggregator.Aggregate(
                points.Select(point => new ChartBucketAggregator.RawPoint(
                    point.Timestamp,
                    DeniedBreakdownFormatter.GetDeniedCategoryValue(point, suffix))),
                from,
                now,
                bucketCount,
                ChartBucketAggregator.AggregationMode.Sum,
                storageBucketDuration);

            var deniedChartPoints = deniedAgg.Buckets
                .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
                .ToList();

            series.Add(new ClientAreaSeries(
                targetId + suffix,
                label,
                deniedChartPoints,
                Hidden: true));
        }
    }

    private static IEnumerable<(string Suffix, string Label)> GetTripletDefinitions(
        DeniedViewMode mode,
        IStringLocalizer<SharedResources> localizer) =>
        mode == DeniedViewMode.CapacityDenied
            ?
            [
                (ChartAggregator.DeniedUnauthSuffix, localizer[TermKeys.DeniedUnauthenticated]),
                (ChartAggregator.DeniedBlockedSuffix, localizer[TermKeys.DeniedBlocked]),
                (ChartAggregator.DeniedCapacitySuffix, localizer[TermKeys.DeniedOutOfSlots])
            ]
            :
            [
                (ChartAggregator.DeniedUnauthSuffix, localizer[TermKeys.DeniedUnauthenticated]),
                (ChartAggregator.DeniedBlockedSuffix, localizer[TermKeys.DeniedBlocked]),
                (ChartAggregator.DeniedRateLimitedSuffix, localizer[TermKeys.DeniedThrottled])
            ];
}
