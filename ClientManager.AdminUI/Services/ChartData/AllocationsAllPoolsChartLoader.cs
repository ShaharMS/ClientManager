using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class AllocationsAllPoolsChartLoader
{
    private readonly StatisticsApiService _statsService;

    public AllocationsAllPoolsChartLoader(StatisticsApiService statsService) => _statsService = statsService;

    public async Task<(List<TargetChartData> Charts, List<AllocationClientRow> Rows)> LoadAsync(
        AllocationsLoadContext context,
        List<ResourcePoolStatisticsResponse> visiblePools,
        IReadOnlyList<TargetClientUsageBreakdownResponse> breakdowns,
        IReadOnlyList<TargetClientUsageBreakdownResponse> recentBreakdowns,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        ChartBucketAggregator.AggregationMode chartAggregationMode,
        ChartBucketAggregator.AggregationResult chartTemplate,
        TimeSpan chartBucketDuration,
        DateTime from,
        DateTime now)
    {
        var charts = new List<TargetChartData>();
        var rows = new List<AllocationClientRow>();
        var totalMaxSlots = 0;
        var visiblePoolIds = visiblePools.Select(p => p.ResourcePoolId).ToList();

        var allHistories = await _statsService.GetHistoricalUsageAsync(
            "ResourcePool", visiblePoolIds, null, from, now, context.TimeRange.Granularity);

        foreach (var pool in visiblePools)
        {
            totalMaxSlots += AllocationsCapCalculator.GetPoolChartCap(
                pool, context.IsAccessMetric, rateLimitLookup, chartBucketDuration);

            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == pool.ResourcePoolId);
            var recentEntries = recentBreakdowns
                .FirstOrDefault(b => b.TargetId == pool.ResourcePoolId)?.Entries ?? [];

            foreach (var entry in breakdown?.Entries ?? [])
            {
                var recentEntry = recentEntries.FirstOrDefault(e => e.ClientId == entry.ClientId);
                rows.Add(AllocationsClientRowFactory.Create(
                    context, entry.ClientId, entry.ClientName, pool, recentEntry, rateLimitLookup));
            }
        }

        var aggregateLabel = context.IsAccessMetric ? "All Pools (Access)" : "All Pools";
        var targetPointLists = visiblePools
            .Select(pool => (IReadOnlyList<HistoricalUsagePoint>)(allHistories.FirstOrDefault(h => h.TargetId == pool.ResourcePoolId)?.Points ?? []));
        var (clientAreas, referenceBuckets) = AggregateTargetChartSeriesBuilder.Build(
            targetPointLists,
            context.IsAccessMetric,
            aggregateLabel,
            context.IsAccessMetric ? DeniedViewMode.RateLimitDenied : DeniedViewMode.CapacityDenied,
            from,
            now,
            context.BucketCount);

        var capPoints = referenceBuckets
            .Select(bucket => new ChartPoint(bucket.Label, totalMaxSlots))
            .ToList();
        charts.Add(new TargetChartData("All Pools", clientAreas, capPoints));

        return (charts, rows);
    }
}
