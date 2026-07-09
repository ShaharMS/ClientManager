using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class DashboardAllTargetsChartLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly ResourcePoolApiService _poolService;
    private readonly GlobalRateLimitApiService _rateLimitApi;
    private readonly IStringLocalizer<SharedResources> _localizer;

    private AllTargetsFetchCache? _cache;

    private sealed record AllTargetsFetchCache(
        string CacheKey,
        List<HistoricalUsageResponse> Histories,
        List<NamedItem> Targets,
        Dictionary<string, GlobalRateLimit> RateLimitLookup,
        List<ResourcePool> PoolTargets,
        string AggregateLabel,
        bool IsRateBased,
        DateTime From,
        DateTime To,
        string Granularity,
        IReadOnlyList<TargetClientUsageBreakdownResponse> Breakdowns,
        Dictionary<string, Dictionary<string, ClientHistoricalUsageResponse>> ClientHistoriesByService);

    public DashboardAllTargetsChartLoader(
        StatisticsApiService statsService,
        ResourcePoolApiService poolService,
        GlobalRateLimitApiService rateLimitApi,
        IStringLocalizer<SharedResources> localizer)
    {
        _statsService = statsService;
        _poolService = poolService;
        _rateLimitApi = rateLimitApi;
        _localizer = localizer;
    }

    public async Task LoadAsync(DashboardChartLoadContext context, List<TargetChartData> charts)
    {
        _cache = await FetchAsync(context);
        BuildCharts(_cache, context, charts);
    }

    public bool TryRebuildFromCache(DashboardChartLoadContext context, List<TargetChartData> charts)
    {
        if (_cache is null || _cache.CacheKey != BuildCacheKey(context))
        {
            return false;
        }

        BuildCharts(_cache, context, charts);
        return true;
    }

    private static string BuildCacheKey(DashboardChartLoadContext context) =>
        $"{context.SelectedFilterType}|all|{context.TimeRange.GetFrom():O}|{context.TimeRange.GetTo():O}|{context.TimeRange.Granularity}";

    private async Task<AllTargetsFetchCache> FetchAsync(DashboardChartLoadContext context)
    {
        var now = context.TimeRange.GetTo();
        var from = context.TimeRange.GetFrom(now);
        var isRateBased = context.SelectedFilterType == "Service";
        var chartAggregationMode = ChartValueHelper.GetAggregationMode(isRateBased);
        var storageDuration = ChartGranularityHelper.GetStorageBucketDuration(context.TimeRange.Granularity);
        var emptyAggregation = ChartBucketAggregator.Aggregate([], from, now, context.BucketCount, chartAggregationMode, storageDuration);

        var targets = (context.SelectedFilterType == "Service"
            ? context.AllServices
            : context.AllPools).Where(t => t.Id != DashboardChartLoadContext.AllTargetsId).ToList();

        var targetIds = targets.Select(t => t.Id).ToList();
        var rateLimitLookup = new Dictionary<string, GlobalRateLimit>(StringComparer.Ordinal);
        List<ResourcePool> poolTargets = [];

        if (isRateBased)
        {
            var rateLimits = await _rateLimitApi.GetByTargetTypeAsync(TargetType.Service);
            foreach (var target in targets)
            {
                var limit = rateLimits.FirstOrDefault(r => r.TargetId == target.Id);
                if (limit is not null)
                {
                    rateLimitLookup[target.Id] = limit;
                }
            }
        }
        else
        {
            poolTargets = await _poolService.GetAllAsync();
        }

        var histories = await _statsService.GetHistoricalUsageAsync(
            context.SelectedFilterType, targetIds, null, from, now, context.TimeRange.Granularity);

        var breakdowns = isRateBased
            ? await _statsService.GetClientUsageBreakdownAsync(
                context.SelectedFilterType, targetIds, null, from, now, context.TimeRange.Granularity)
            : [];

        var clientHistoriesByService = new Dictionary<string, Dictionary<string, ClientHistoricalUsageResponse>>(StringComparer.Ordinal);
        if (isRateBased)
        {
            foreach (var target in targets)
            {
                var entries = breakdowns.FirstOrDefault(b => b.TargetId == target.Id)?.Entries ?? [];
                clientHistoriesByService[target.Id] = entries.Count > 0
                    ? (await _statsService.GetHistoricalUsageByClientAsync(
                        context.SelectedFilterType,
                        new[] { target.Id },
                        entries.Select(entry => entry.ClientId),
                        from,
                        now,
                        context.TimeRange.Granularity))
                    .ToDictionary(history => history.ClientId)
                    : [];
            }
        }

        var aggregateLabel = isRateBased
            ? _localizer["Pages.Dashboard.Target.AllServices"]
            : _localizer["Pages.Dashboard.Target.AllResourcePools"];

        return new AllTargetsFetchCache(
            BuildCacheKey(context),
            histories,
            targets,
            rateLimitLookup,
            poolTargets,
            aggregateLabel,
            isRateBased,
            from,
            now,
            context.TimeRange.Granularity,
            breakdowns,
            clientHistoriesByService);
    }

    private void BuildCharts(AllTargetsFetchCache cache, DashboardChartLoadContext context, List<TargetChartData> charts)
    {
        charts.Clear();
        var storageDuration = ChartGranularityHelper.GetStorageBucketDuration(cache.Granularity);
        var chartAggregationMode = ChartValueHelper.GetAggregationMode(cache.IsRateBased);
        var emptyAggregation = ChartBucketAggregator.Aggregate(
            [], cache.From, cache.To, context.BucketCount, chartAggregationMode, storageDuration);

        if (cache.IsRateBased)
        {
            BuildServiceCharts(cache, context, charts, storageDuration, emptyAggregation.BucketDuration);
            return;
        }

        // ponytail: resource pools have no off-budget concept; target-level aggregate only
        var targetPointLists = cache.Targets
            .Select(target => (IReadOnlyList<HistoricalUsagePoint>)(cache.Histories.FirstOrDefault(h => h.TargetId == target.Id)?.Points ?? []));
        var (clientAreas, referenceBuckets) = AggregateTargetChartSeriesBuilder.Build(
            targetPointLists,
            cache.IsRateBased,
            cache.AggregateLabel,
            DeniedViewMode.CapacityDenied,
            cache.From,
            cache.To,
            context.BucketCount,
            _localizer,
            storageDuration);

        var poolStats = cache.Targets
            .Select(target => cache.PoolTargets.FirstOrDefault(pool => pool.Id == target.Id))
            .Where(pool => pool is not null)
            .Select(pool => new ResourcePoolStatisticsResponse(
                pool!.Id, pool.Name, (int)pool.MaxSlots, 0, (int)pool.MaxSlots, false))
            .ToList();
        var chartCap = ChartCapResolver.ResolveAllPoolsSlotCap(poolStats);
        var capPoints = ChartCapResolver.BuildCapSeries(referenceBuckets, chartCap);

        charts.Add(new TargetChartData(cache.AggregateLabel, clientAreas, capPoints));
    }

    private void BuildServiceCharts(
        AllTargetsFetchCache cache,
        DashboardChartLoadContext context,
        List<TargetChartData> charts,
        TimeSpan storageDuration,
        TimeSpan chartBucketDuration)
    {
        var services = cache.Targets
            .Select(target => new Service { Id = target.Id, Name = target.Name })
            .ToList();
        var chartCap = ChartCapResolver.ResolveAllServicesChartCap(
            services, cache.RateLimitLookup, chartBucketDuration);

        var (contributingRaw, offBudgetRaw) = OffBudgetChartSeriesBuilder.PartitionClientHistories(
            services, cache.Breakdowns, cache.ClientHistoriesByService, context.AllClients);

        var contributingAgg = ChartBucketAggregator.Aggregate(
            contributingRaw,
            cache.From,
            cache.To,
            context.BucketCount,
            ChartBucketAggregator.AggregationMode.Sum,
            storageDuration);
        var offBudgetAgg = ChartBucketAggregator.Aggregate(
            offBudgetRaw,
            cache.From,
            cache.To,
            context.BucketCount,
            ChartBucketAggregator.AggregationMode.Sum,
            storageDuration);

        var referenceBuckets = contributingAgg.Buckets.Count > 0
            ? contributingAgg.Buckets
            : offBudgetAgg.Buckets.Count > 0
                ? offBudgetAgg.Buckets
                : ChartBucketAggregator.Aggregate([], cache.From, cache.To, context.BucketCount).Buckets;

        var clientAreas = new List<ClientAreaSeries>();
        var chartPoints = contributingAgg.Buckets
            .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
            .ToList();
        if (chartPoints.Any(point => point.Value > 0))
        {
            clientAreas.Add(new ClientAreaSeries(ChartAggregator.AggregateSeriesId, cache.AggregateLabel, chartPoints));
        }

        var offBudgetPoints = offBudgetAgg.Buckets
            .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
            .ToList();
        OffBudgetChartSeriesBuilder.AppendSeries(
            clientAreas, ChartAggregator.AggregateSeriesId, offBudgetPoints, _localizer);

        var targetPointLists = cache.Targets
            .Select(target => (IReadOnlyList<HistoricalUsagePoint>)(cache.Histories.FirstOrDefault(h => h.TargetId == target.Id)?.Points ?? []));
        var mergedHistory = HistoricalPointMerger.SumByTimestamp(targetPointLists);
        DeniedChartSeriesBuilder.AppendTripletSeries(
            clientAreas,
            ChartAggregator.AggregateSeriesId,
            mergedHistory,
            DeniedViewMode.RateLimitDenied,
            cache.From,
            cache.To,
            context.BucketCount,
            _localizer,
            storageDuration);

        var capPoints = ChartCapResolver.BuildCapSeries(referenceBuckets, chartCap);

        charts.Add(new TargetChartData(cache.AggregateLabel, clientAreas, capPoints));
    }
}
