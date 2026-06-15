using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

public sealed class AllocationsDataLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly GlobalRateLimitApiService _rateLimitApi;
    private readonly AllocationsAllPoolsChartLoader _allPoolsLoader;
    private readonly AllocationsSinglePoolChartLoader _singlePoolLoader;

    public AllocationsDataLoader(StatisticsApiService statsService, GlobalRateLimitApiService rateLimitApi)
    {
        _statsService = statsService;
        _rateLimitApi = rateLimitApi;
        _allPoolsLoader = new AllocationsAllPoolsChartLoader(statsService);
        _singlePoolLoader = new AllocationsSinglePoolChartLoader(statsService);
    }

    public async Task<AllocationsLoadResult> LoadAsync(AllocationsLoadContext context)
    {
        var newPools = await _statsService.GetResourcePoolStatsAsync();

        var now = context.TimeRange.GetTo();
        var from = context.TimeRange.GetFrom(now);
        var granularity = context.TimeRange.Granularity;
        var recentFrom = now.Subtract(AllocationsLoadContext.RecentWindow);
        var recentTo = now;
        var chartAggregationMode = context.IsAccessMetric
            ? ChartBucketAggregator.AggregationMode.Sum
            : ChartBucketAggregator.AggregationMode.Latest;
        var chartTemplate = ChartBucketAggregator.Aggregate([], from, now, context.BucketCount, chartAggregationMode);
        var chartBucketDuration = chartTemplate.BucketDuration;

        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup = new Dictionary<string, GlobalRateLimit>();
        if (context.IsAccessMetric)
        {
            rateLimitLookup = (await _rateLimitApi.GetByTargetTypeAsync(TargetType.ResourcePool))
                .ToDictionary(limit => limit.TargetId);
        }

        var visiblePools = context.SelectedPoolId == AllocationsLoadContext.AllPoolsId
            ? newPools
            : newPools.Where(p => p.ResourcePoolId == context.SelectedPoolId).ToList();

        var visiblePoolIds = visiblePools.Select(p => p.ResourcePoolId).ToList();

        if (visiblePoolIds.Count == 0)
        {
            return new AllocationsLoadResult(
                [],
                [],
                newPools,
                AllocationsPoolSummaryBuilder.Build(newPools, [], rateLimitLookup, context.IsAccessMetric));
        }

        var breakdowns = await _statsService.GetClientUsageBreakdownAsync(
            "ResourcePool", visiblePoolIds, context.SelectedClientIds, from, now, granularity);
        var recentBreakdowns = await _statsService.GetClientUsageBreakdownAsync(
            "ResourcePool", visiblePoolIds, context.SelectedClientIds, recentFrom, recentTo, "FiveMinute");

        var summaryRecentBreakdowns = recentBreakdowns;
        if (context.IsAccessMetric && context.SelectedPoolId != AllocationsLoadContext.AllPoolsId)
        {
            summaryRecentBreakdowns = await _statsService.GetClientUsageBreakdownAsync(
                "ResourcePool",
                newPools.Select(pool => pool.ResourcePoolId),
                context.SelectedClientIds,
                recentFrom,
                recentTo,
                "FiveMinute");
        }

        var poolRows = AllocationsPoolSummaryBuilder.Build(
            newPools, summaryRecentBreakdowns, rateLimitLookup, context.IsAccessMetric);

        if (context.SelectedPoolId == AllocationsLoadContext.AllPoolsId)
        {
            var (charts, clientRows) = await _allPoolsLoader.LoadAsync(
                context, visiblePools, breakdowns, recentBreakdowns,
                rateLimitLookup, chartAggregationMode, chartTemplate, chartBucketDuration, from, now);
            return new AllocationsLoadResult(charts, clientRows, newPools, poolRows);
        }

        var (singleCharts, singleRows) = await _singlePoolLoader.LoadAsync(
            context, visiblePools, breakdowns, recentBreakdowns,
            rateLimitLookup, chartAggregationMode, chartTemplate, chartBucketDuration, from, now);
        return new AllocationsLoadResult(singleCharts, singleRows, newPools, poolRows);
    }
}
