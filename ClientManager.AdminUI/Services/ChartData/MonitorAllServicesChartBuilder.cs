using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class MonitorAllServicesChartBuilder
{
    internal static void Build(
        MonitorLoadContext context,
        List<Service> visibleServices,
        IReadOnlyList<TargetClientUsageBreakdownResponse> breakdowns,
        IReadOnlyList<HistoricalUsageResponse> allHistories,
        IReadOnlyDictionary<string, Dictionary<string, ClientHistoricalUsageResponse>> clientHistoriesByService,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        TimeSpan chartBucketDuration,
        TimeSpan rangeDuration,
        DateTime from,
        DateTime now,
        List<TargetChartData> charts,
        List<MonitorClientRow> rows,
        IStringLocalizer<SharedResources> localizer,
        TimeSpan storageBucketDuration)
    {
        var totalCap = ChartCapResolver.ResolveAllServicesChartCap(
            visibleServices, rateLimitLookup, chartBucketDuration);

        foreach (var service in visibleServices)
        {
            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == service.Id);

            foreach (var entry in breakdown?.Entries ?? [])
            {
                rows.Add(new MonitorClientRow(
                    entry.ClientId, entry.ClientName, service.Name,
                    entry.GrantedCount,
                    entry.DeniedCount,
                    entry.DeniedUnauthenticatedCount,
                    entry.DeniedBlockedCount,
                    entry.DeniedRateLimitedCount,
                    entry.DeniedCapacityLimitedCount,
                    MonitorCapCalculator.GetEffectiveClientServiceCap(
                        entry.ClientId, service.Id, context.AllClients, rateLimitLookup, rangeDuration)));
            }
        }

        var (contributingRaw, offBudgetRaw) = OffBudgetChartSeriesBuilder.PartitionClientHistories(
            visibleServices, breakdowns, clientHistoriesByService, context.AllClients);

        var contributingAgg = ChartBucketAggregator.Aggregate(
            contributingRaw,
            from,
            now,
            context.BucketCount,
            ChartBucketAggregator.AggregationMode.Sum,
            storageBucketDuration);
        var offBudgetAgg = ChartBucketAggregator.Aggregate(
            offBudgetRaw,
            from,
            now,
            context.BucketCount,
            ChartBucketAggregator.AggregationMode.Sum,
            storageBucketDuration);

        var referenceBuckets = contributingAgg.Buckets.Count > 0
            ? contributingAgg.Buckets
            : offBudgetAgg.Buckets.Count > 0
                ? offBudgetAgg.Buckets
                : ChartBucketAggregator.Aggregate([], from, now, context.BucketCount).Buckets;

        var allServicesLabel = localizer["Pages.Monitor.Chart.AllServices"];
        var clientAreas = new List<ClientAreaSeries>();

        var chartPoints = contributingAgg.Buckets
            .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
            .ToList();
        if (chartPoints.Any(point => point.Value > 0))
        {
            clientAreas.Add(new ClientAreaSeries(ChartAggregator.AggregateSeriesId, allServicesLabel, chartPoints));
        }

        var offBudgetPoints = offBudgetAgg.Buckets
            .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
            .ToList();
        OffBudgetChartSeriesBuilder.AppendSeries(
            clientAreas, ChartAggregator.AggregateSeriesId, offBudgetPoints, localizer);

        var targetPointLists = visibleServices
            .Select(service => (IReadOnlyList<HistoricalUsagePoint>)(allHistories.FirstOrDefault(h => h.TargetId == service.Id)?.Points ?? []));
        var mergedHistory = HistoricalPointMerger.SumByTimestamp(targetPointLists);
        DeniedChartSeriesBuilder.AppendTripletSeries(
            clientAreas,
            ChartAggregator.AggregateSeriesId,
            mergedHistory,
            DeniedViewMode.RateLimitDenied,
            from,
            now,
            context.BucketCount,
            localizer,
            storageBucketDuration);

        var capPoints = ChartCapResolver.BuildCapSeries(referenceBuckets, totalCap);

        charts.Add(new TargetChartData(allServicesLabel, clientAreas, capPoints));
    }
}
