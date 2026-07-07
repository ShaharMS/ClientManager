using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

public sealed class AllocationsDataLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly GlobalRateLimitApiService _rateLimitApi;
    private readonly AllocationsAllPoolsChartLoader _allPoolsLoader;
    private readonly AllocationsSinglePoolChartLoader _singlePoolLoader;

    private AllocationsFetchCache? _cache;

    public AllocationsDataLoader(
        StatisticsApiService statsService,
        GlobalRateLimitApiService rateLimitApi,
        IStringLocalizer<SharedResources> localizer)
    {
        _statsService = statsService;
        _rateLimitApi = rateLimitApi;
        _allPoolsLoader = new AllocationsAllPoolsChartLoader(statsService, localizer);
        _singlePoolLoader = new AllocationsSinglePoolChartLoader(statsService, localizer);
    }

    public async Task<AllocationsLoadResult> LoadAsync(AllocationsLoadContext context)
    {
        _cache = await FetchAsync(context);
        return BuildFromCache(context);
    }

    public bool TryRebuildFromCache(AllocationsLoadContext context, out AllocationsLoadResult result)
    {
        if (_cache is null || _cache.CacheKey != BuildCacheKey(context))
        {
            result = new AllocationsLoadResult([], [], [], []);
            return false;
        }

        result = BuildFromCache(context);
        return true;
    }

    private static string BuildCacheKey(AllocationsLoadContext context) =>
        $"{context.SelectedPoolId}|{string.Join(',', context.SelectedClientIds ?? [])}|{context.TimeRange.GetFrom():O}|{context.TimeRange.GetTo():O}|{context.TimeRange.Granularity}|{context.IsAccessMetric}";

    private async Task<AllocationsFetchCache> FetchAsync(AllocationsLoadContext context)
    {
        var newPools = await _statsService.GetResourcePoolStatsAsync();
        var now = context.TimeRange.GetTo();
        var from = context.TimeRange.GetFrom(now);
        var granularity = context.TimeRange.Granularity;
        var recentFrom = now.Subtract(AllocationsLoadContext.RecentWindow);
        var recentTo = now;

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
        var isAllPools = context.SelectedPoolId == AllocationsLoadContext.AllPoolsId;

        List<TargetClientUsageBreakdownResponse> breakdowns = [];
        List<TargetClientUsageBreakdownResponse> recentBreakdowns = [];
        List<HistoricalUsageResponse> allHistories = [];
        var clientHistoriesByPool = new Dictionary<string, Dictionary<string, ClientHistoricalUsageResponse>>(StringComparer.Ordinal);
        var poolHistories = new Dictionary<string, HistoricalUsageResponse?>(StringComparer.Ordinal);

        if (visiblePoolIds.Count > 0)
        {
            breakdowns = await _statsService.GetClientUsageBreakdownAsync(
                "ResourcePool", visiblePoolIds, context.SelectedClientIds, from, now, granularity);
            recentBreakdowns = await _statsService.GetClientUsageBreakdownAsync(
                "ResourcePool", visiblePoolIds, context.SelectedClientIds, recentFrom, recentTo, "FiveMinute");

            if (isAllPools)
            {
                allHistories = await _statsService.GetHistoricalUsageAsync(
                    "ResourcePool", visiblePoolIds, null, from, now, granularity);
            }
            else
            {
                foreach (var pool in visiblePools)
                {
                    var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == pool.ResourcePoolId);
                    var entries = breakdown?.Entries ?? [];
                    var historiesByClientId = (await _statsService.GetHistoricalUsageByClientAsync(
                            "ResourcePool",
                            new[] { pool.ResourcePoolId },
                            entries.Select(entry => entry.ClientId),
                            from,
                            now,
                            granularity))
                        .ToDictionary(history => history.ClientId);
                    clientHistoriesByPool[pool.ResourcePoolId] = historiesByClientId;
                    poolHistories[pool.ResourcePoolId] = (await _statsService.GetHistoricalUsageAsync(
                        "ResourcePool", new[] { pool.ResourcePoolId }, null, from, now, granularity))
                        .FirstOrDefault();
                }
            }
        }

        var summaryRecentBreakdowns = recentBreakdowns;
        if (context.IsAccessMetric && !isAllPools)
        {
            summaryRecentBreakdowns = await _statsService.GetClientUsageBreakdownAsync(
                "ResourcePool",
                newPools.Select(pool => pool.ResourcePoolId),
                context.SelectedClientIds,
                recentFrom,
                recentTo,
                "FiveMinute");
        }

        return new AllocationsFetchCache(
            BuildCacheKey(context),
            newPools,
            breakdowns,
            recentBreakdowns,
            summaryRecentBreakdowns,
            rateLimitLookup.ToDictionary(),
            visiblePools,
            isAllPools,
            allHistories,
            clientHistoriesByPool,
            poolHistories,
            from,
            now,
            granularity,
            context.IsAccessMetric);
    }

    private AllocationsLoadResult BuildFromCache(AllocationsLoadContext context)
    {
        if (_cache is null)
        {
            return new AllocationsLoadResult([], [], [], []);
        }

        var chartAggregationMode = context.IsAccessMetric
            ? ChartBucketAggregator.AggregationMode.Sum
            : ChartBucketAggregator.AggregationMode.Latest;
        var storageDuration = ChartGranularityHelper.GetStorageBucketDuration(_cache.Granularity);
        var chartTemplate = ChartBucketAggregator.Aggregate(
            [], _cache.From, _cache.Now, context.BucketCount, chartAggregationMode, storageDuration);
        var chartBucketDuration = chartTemplate.BucketDuration;

        var poolRows = AllocationsPoolSummaryBuilder.Build(
            _cache.Pools, _cache.SummaryRecentBreakdowns, _cache.RateLimitLookup, context.IsAccessMetric);

        if (_cache.VisiblePools.Count == 0)
        {
            return new AllocationsLoadResult([], [], _cache.Pools, poolRows);
        }

        if (_cache.IsAllPools)
        {
            var (charts, clientRows) = _allPoolsLoader.BuildFromCache(
                context, _cache, chartAggregationMode, chartTemplate, chartBucketDuration, storageDuration);
            return new AllocationsLoadResult(charts, clientRows, _cache.Pools, poolRows);
        }

        var (singleCharts, singleRows) = _singlePoolLoader.BuildFromCache(
            context, _cache, chartAggregationMode, chartTemplate, chartBucketDuration, storageDuration);
        return new AllocationsLoadResult(singleCharts, singleRows, _cache.Pools, poolRows);
    }
}
