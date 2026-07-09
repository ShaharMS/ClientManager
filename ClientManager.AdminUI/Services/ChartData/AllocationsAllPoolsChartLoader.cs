using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class AllocationsAllPoolsChartLoader
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AllocationsAllPoolsChartLoader(
        StatisticsApiService statsService,
        IStringLocalizer<SharedResources> localizer)
    {
        _ = statsService;
        _localizer = localizer;
    }

    public (List<TargetChartData> Charts, List<AllocationClientRow> Rows) BuildFromCache(
        AllocationsLoadContext context,
        AllocationsFetchCache cache,
        ChartBucketAggregator.AggregationMode chartAggregationMode,
        ChartBucketAggregator.AggregationResult chartTemplate,
        TimeSpan chartBucketDuration,
        TimeSpan storageDuration)
    {
        var charts = new List<TargetChartData>();
        var rows = new List<AllocationClientRow>();

        foreach (var pool in cache.VisiblePools)
        {
            var breakdown = cache.Breakdowns.FirstOrDefault(b => b.TargetId == pool.ResourcePoolId);
            var recentEntries = cache.RecentBreakdowns
                .FirstOrDefault(b => b.TargetId == pool.ResourcePoolId)?.Entries ?? [];

            foreach (var entry in breakdown?.Entries ?? [])
            {
                var recentEntry = recentEntries.FirstOrDefault(e => e.ClientId == entry.ClientId);
                rows.Add(AllocationsClientRowFactory.Create(
                    context, entry.ClientId, entry.ClientName, pool, recentEntry, cache.RateLimitLookup));
            }
        }

        var allPoolsLabel = _localizer["Pages.Allocations.Target.AllPools"];

        if (context.IsAccessMetric)
        {
            BuildAccessMetricChart(
                context, cache, allPoolsLabel, chartBucketDuration, storageDuration, charts);
            return (charts, rows);
        }

        var targetPointLists = cache.VisiblePools
            .Select(pool => (IReadOnlyList<HistoricalUsagePoint>)(cache.AllHistories
                .FirstOrDefault(h => h.TargetId == pool.ResourcePoolId)?.Points ?? []));
        var (clientAreas, referenceBuckets) = AggregateTargetChartSeriesBuilder.Build(
            targetPointLists,
            context.IsAccessMetric,
            allPoolsLabel,
            DeniedViewMode.CapacityDenied,
            cache.From,
            cache.Now,
            context.BucketCount,
            _localizer,
            storageDuration);

        var chartCap = ChartCapResolver.ResolveAllPoolsSlotCap(cache.VisiblePools);
        var capPoints = ChartCapResolver.BuildCapSeries(referenceBuckets, chartCap);
        charts.Add(new TargetChartData(allPoolsLabel, clientAreas, capPoints));

        return (charts, rows);
    }

    private void BuildAccessMetricChart(
        AllocationsLoadContext context,
        AllocationsFetchCache cache,
        string allPoolsLabel,
        TimeSpan chartBucketDuration,
        TimeSpan storageDuration,
        List<TargetChartData> charts)
    {
        var (contributingRaw, offBudgetRaw) = OffBudgetChartSeriesBuilder.PartitionClientHistoriesForPools(
            cache.VisiblePools, cache.Breakdowns, cache.ClientHistoriesByPool, context.AllClients);

        var contributingAgg = ChartBucketAggregator.Aggregate(
            contributingRaw,
            cache.From,
            cache.Now,
            context.BucketCount,
            ChartBucketAggregator.AggregationMode.Sum,
            storageDuration);
        var offBudgetAgg = ChartBucketAggregator.Aggregate(
            offBudgetRaw,
            cache.From,
            cache.Now,
            context.BucketCount,
            ChartBucketAggregator.AggregationMode.Sum,
            storageDuration);

        var referenceBuckets = contributingAgg.Buckets.Count > 0
            ? contributingAgg.Buckets
            : offBudgetAgg.Buckets.Count > 0
                ? offBudgetAgg.Buckets
                : ChartBucketAggregator.Aggregate([], cache.From, cache.Now, context.BucketCount).Buckets;

        var clientAreas = new List<ClientAreaSeries>();
        var chartPoints = contributingAgg.Buckets
            .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
            .ToList();
        if (chartPoints.Any(point => point.Value > 0))
        {
            clientAreas.Add(new ClientAreaSeries(ChartAggregator.AggregateSeriesId, allPoolsLabel, chartPoints));
        }

        var offBudgetPoints = offBudgetAgg.Buckets
            .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
            .ToList();
        OffBudgetChartSeriesBuilder.AppendSeries(
            clientAreas, ChartAggregator.AggregateSeriesId, offBudgetPoints, _localizer);

        var targetPointLists = cache.VisiblePools
            .Select(pool => (IReadOnlyList<HistoricalUsagePoint>)(cache.AllHistories
                .FirstOrDefault(h => h.TargetId == pool.ResourcePoolId)?.Points ?? []));
        var mergedHistory = HistoricalPointMerger.SumByTimestamp(targetPointLists);
        DeniedChartSeriesBuilder.AppendTripletSeries(
            clientAreas,
            ChartAggregator.AggregateSeriesId,
            mergedHistory,
            DeniedViewMode.RateLimitDenied,
            cache.From,
            cache.Now,
            context.BucketCount,
            _localizer,
            storageDuration);

        var chartCap = ChartCapResolver.ResolveAllPoolsAccessChartCap(
            cache.VisiblePools, cache.RateLimitLookup, chartBucketDuration);
        var capPoints = ChartCapResolver.BuildCapSeries(referenceBuckets, chartCap);
        charts.Add(new TargetChartData(allPoolsLabel, clientAreas, capPoints));
    }
}
