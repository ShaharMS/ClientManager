using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.AdminUI.Services.ChartData;

public sealed class MonitorDataLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly GlobalRateLimitApiService _rateLimitApi;
    private readonly MonitorSingleServiceChartLoader _singleServiceLoader;

    public MonitorDataLoader(StatisticsApiService statsService, GlobalRateLimitApiService rateLimitApi)
    {
        _statsService = statsService;
        _rateLimitApi = rateLimitApi;
        _singleServiceLoader = new MonitorSingleServiceChartLoader(statsService);
    }

    public async Task<MonitorLoadResult> LoadAsync(MonitorLoadContext context)
    {
        var now = DateTime.UtcNow;
        var from = context.TimeRange.GetFrom(now);
        var rangeDuration = now - from;
        var chartTemplate = ChartBucketAggregator.Aggregate([], from, now, context.BucketCount);
        var chartBucketDuration = chartTemplate.BucketDuration;

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

        var charts = new List<TargetChartData>();
        var rows = new List<MonitorClientRow>();

        if (context.SelectedServiceId == MonitorLoadContext.AllServicesId)
        {
            MonitorAllServicesChartBuilder.Build(
                context, visibleServices, breakdowns,
                allHistories, rateLimitLookup, chartBucketDuration, rangeDuration, from, now, charts, rows);
        }
        else
        {
            await _singleServiceLoader.LoadAsync(
                context, visibleServices, breakdowns,
                rateLimitLookup, chartTemplate, chartBucketDuration, rangeDuration, from, now, charts, rows);
        }

        var allServiceIds = context.AllServices.Select(s => s.Id).ToList();
        var recentFrom = now.Subtract(MonitorLoadContext.RecentWindow);
        var summaryBreakdowns = await _statsService.GetClientUsageBreakdownAsync(
            "Service", allServiceIds, null, recentFrom, now, "FiveMinute");

        var serviceStats = context.AllServices.Select(service =>
        {
            var bd = summaryBreakdowns.FirstOrDefault(b => b.TargetId == service.Id);
            var totalUsage = bd?.Entries.Sum(e => e.GrantedCount) ?? 0;
            var cap = MonitorCapCalculator.GetScaledGlobalServiceCap(
                service.Id, rateLimitLookup, MonitorLoadContext.RecentWindow);
            var entries = bd?.Entries ?? [];
            return new ServiceSummaryRow(
                service.Id,
                service.Name,
                totalUsage,
                cap,
                entries.Sum(e => e.DeniedCount),
                entries.Sum(e => e.DeniedUnauthenticatedCount),
                entries.Sum(e => e.DeniedBlockedCount),
                entries.Sum(e => e.DeniedRateLimitedCount),
                entries.Sum(e => e.DeniedCapacityLimitedCount));
        }).ToList();

        return new MonitorLoadResult(charts, rows, serviceStats);
    }
}
