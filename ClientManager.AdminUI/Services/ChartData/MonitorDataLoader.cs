using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

public sealed class MonitorDataLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly GlobalRateLimitApiService _rateLimitApi;
    private readonly MonitorSingleServiceChartLoader _singleServiceLoader;
    private readonly IStringLocalizer<SharedResources> _localizer;

    private MonitorFetchCache? _cache;
    private List<ServiceSummaryRow> _lastServiceStats = [];

    public MonitorDataLoader(
        StatisticsApiService statsService,
        GlobalRateLimitApiService rateLimitApi,
        IStringLocalizer<SharedResources> localizer)
    {
        _statsService = statsService;
        _rateLimitApi = rateLimitApi;
        _localizer = localizer;
        _singleServiceLoader = new MonitorSingleServiceChartLoader(statsService, localizer);
    }

    public async Task<MonitorLoadResult> LoadAsync(MonitorLoadContext context)
    {
        _cache = await FetchAsync(context);
        _lastServiceStats = await FetchServiceStatsAsync(context, _cache);
        return BuildChartsFromCache(context);
    }

    public bool TryRebuildFromCache(MonitorLoadContext context, out MonitorLoadResult result)
    {
        if (_cache is null || _cache.CacheKey != BuildCacheKey(context))
        {
            result = new MonitorLoadResult([], [], []);
            return false;
        }

        result = BuildChartsFromCache(context);
        return true;
    }

    private static string BuildCacheKey(MonitorLoadContext context) =>
        $"{context.SelectedServiceId}|{string.Join(',', context.SelectedClientIds ?? [])}|{context.TimeRange.GetFrom():O}|{context.TimeRange.GetTo():O}|{context.TimeRange.Granularity}";

    private async Task<MonitorFetchCache> FetchAsync(MonitorLoadContext context)
    {
        var now = DateTime.UtcNow;
        var from = context.TimeRange.GetFrom(now);
        var rangeDuration = now - from;

        var rateLimits = await _rateLimitApi.GetByTargetTypeAsync(TargetType.Service);
        var rateLimitLookup = rateLimits.ToDictionary(r => r.TargetId);

        var visibleServices = context.SelectedServiceId == MonitorLoadContext.AllServicesId
            ? context.AllServices
            : context.AllServices.Where(s => s.Id == context.SelectedServiceId).ToList();

        var visibleServiceIds = visibleServices.Select(s => s.Id).ToList();

        var breakdowns = await _statsService.GetClientUsageBreakdownAsync(
            "Service", visibleServiceIds, context.SelectedClientIds,
            from, now, context.TimeRange.Granularity);

        var allClientIds = breakdowns
            .SelectMany(b => b.Entries.Select(e => e.ClientId))
            .Distinct()
            .ToList();

        var allHistories = allClientIds.Count > 0
            ? await _statsService.GetHistoricalUsageAsync(
                "Service", visibleServiceIds, null, from, now, context.TimeRange.Granularity)
            : [];

        var clientHistoriesByService = new Dictionary<string, Dictionary<string, ClientHistoricalUsageResponse>>(StringComparer.Ordinal);
        var serviceHistories = new Dictionary<string, HistoricalUsageResponse?>(StringComparer.Ordinal);

        foreach (var service in visibleServices)
        {
            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == service.Id);
            var entries = breakdown?.Entries ?? [];
            var historiesByClientId = entries.Count > 0
                ? (await _statsService.GetHistoricalUsageByClientAsync(
                    "Service",
                    new[] { service.Id },
                    entries.Select(entry => entry.ClientId),
                    from,
                    now,
                    context.TimeRange.Granularity))
                .ToDictionary(history => history.ClientId)
                : new Dictionary<string, ClientHistoricalUsageResponse>(StringComparer.Ordinal);
            clientHistoriesByService[service.Id] = historiesByClientId;

            if (context.SelectedServiceId != MonitorLoadContext.AllServicesId)
            {
                serviceHistories[service.Id] = allHistories.FirstOrDefault(h => h.TargetId == service.Id);
            }
        }

        return new MonitorFetchCache(
            BuildCacheKey(context),
            breakdowns,
            allHistories,
            rateLimitLookup,
            visibleServices,
            context.SelectedServiceId == MonitorLoadContext.AllServicesId,
            from,
            now,
            rangeDuration,
            context.TimeRange.Granularity,
            clientHistoriesByService,
            serviceHistories);
    }

    private async Task<List<ServiceSummaryRow>> FetchServiceStatsAsync(
        MonitorLoadContext context,
        MonitorFetchCache cache)
    {
        var allServiceIds = context.AllServices.Select(s => s.Id).ToList();
        var recentFrom = cache.Now.Subtract(MonitorLoadContext.RecentWindow);
        var summaryBreakdowns = await _statsService.GetClientUsageBreakdownAsync(
            "Service", allServiceIds, null, recentFrom, cache.Now, "FiveMinute");

        return context.AllServices.Select(service =>
        {
            var bd = summaryBreakdowns.FirstOrDefault(b => b.TargetId == service.Id);
            var entries = bd?.Entries ?? [];
            var (contributingUsage, offBudgetUsage) = MonitorCapCalculator.PartitionServiceUsage(
                entries, service.Id, context.AllClients);
            var (cap, usesGlobalCap) = MonitorCapCalculator.GetServiceSummaryCap(
                service.Id, entries, context.AllClients, cache.RateLimitLookup, MonitorLoadContext.RecentWindow);
            return new ServiceSummaryRow(
                service.Id,
                service.Name,
                contributingUsage,
                offBudgetUsage,
                cap,
                usesGlobalCap,
                entries.Sum(e => e.DeniedCount),
                entries.Sum(e => e.DeniedUnauthenticatedCount),
                entries.Sum(e => e.DeniedBlockedCount),
                entries.Sum(e => e.DeniedRateLimitedCount),
                entries.Sum(e => e.DeniedCapacityLimitedCount));
        }).ToList();
    }

    private MonitorLoadResult BuildChartsFromCache(MonitorLoadContext context)
    {
        if (_cache is null)
        {
            return new MonitorLoadResult([], [], _lastServiceStats);
        }

        var storageDuration = ChartGranularityHelper.GetStorageBucketDuration(_cache.Granularity);
        var chartTemplate = ChartBucketAggregator.Aggregate([], _cache.From, _cache.Now, context.BucketCount, ChartBucketAggregator.AggregationMode.Sum, storageDuration);
        var chartBucketDuration = chartTemplate.BucketDuration;

        var charts = new List<TargetChartData>();
        var rows = new List<MonitorClientRow>();

        if (_cache.IsAllServices)
        {
            MonitorAllServicesChartBuilder.Build(
                context, _cache.VisibleServices, _cache.Breakdowns,
                _cache.AllHistories, _cache.ClientHistoriesByService, _cache.RateLimitLookup,
                chartBucketDuration, _cache.RangeDuration,
                _cache.From, _cache.Now, charts, rows, _localizer, storageDuration);
        }
        else
        {
            _singleServiceLoader.BuildFromCache(
                context, _cache, chartTemplate, chartBucketDuration, charts, rows, storageDuration);
        }

        return new MonitorLoadResult(charts, rows, _lastServiceStats);
    }
}
