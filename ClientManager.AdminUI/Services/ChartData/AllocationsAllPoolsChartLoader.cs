using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Services;
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
        var poolAggregations = new List<ChartBucketAggregator.AggregationResult>();
        var totalMaxSlots = 0;
        var visiblePoolIds = visiblePools.Select(p => p.ResourcePoolId).ToList();

        var allHistories = await _statsService.GetHistoricalUsageAsync(
            "ResourcePool", visiblePoolIds, null, from, now, context.TimeRange.Granularity);

        foreach (var pool in visiblePools)
        {
            totalMaxSlots += AllocationsCapCalculator.GetPoolChartCap(
                pool, context.IsAccessMetric, rateLimitLookup, chartBucketDuration);

            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == pool.ResourcePoolId);
            var history = allHistories.FirstOrDefault(h => h.TargetId == pool.ResourcePoolId);
            var recentEntries = recentBreakdowns
                .FirstOrDefault(b => b.TargetId == pool.ResourcePoolId)?.Entries ?? [];

            var rawPoints = (history?.Points ?? [])
                .Select(point => new ChartBucketAggregator.RawPoint(
                    point.Timestamp,
                    AllocationsChartPointHelper.GetHistoricalPointValue(point, context.IsAccessMetric)))
                .ToList();

            if (rawPoints.Count > 0)
            {
                poolAggregations.Add(ChartBucketAggregator.Aggregate(rawPoints, from, now, mode: chartAggregationMode));
            }

            foreach (var entry in breakdown?.Entries ?? [])
            {
                var recentEntry = recentEntries.FirstOrDefault(e => e.ClientId == entry.ClientId);
                rows.Add(AllocationsClientRowFactory.Create(
                    context, entry.ClientId, entry.ClientName, pool, recentEntry, rateLimitLookup));
            }
        }

        var referenceBuckets = poolAggregations.FirstOrDefault()?.Buckets ?? chartTemplate.Buckets;
        var sortedPoints = referenceBuckets
            .Select((bucket, index) => new ChartPoint(
                bucket.Label,
                poolAggregations.Sum(aggregation => aggregation.Buckets[index].Value)))
            .ToList();

        var capPoints = sortedPoints.Select(p => new ChartPoint(p.Label, totalMaxSlots)).ToList();
        charts.Add(new TargetChartData("All Pools",
            new List<ClientAreaSeries> { new("total", "Total", sortedPoints) },
            capPoints));

        return (charts, rows);
    }
}
