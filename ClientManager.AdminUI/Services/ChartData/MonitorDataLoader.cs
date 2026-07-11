using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Requests;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

public sealed class MonitorDataLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly GlobalRateLimitApiService _rateLimitApi;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public MonitorDataLoader(
        StatisticsApiService statsService,
        GlobalRateLimitApiService rateLimitApi,
        IStringLocalizer<SharedResources> localizer)
    {
        _statsService = statsService;
        _rateLimitApi = rateLimitApi;
        _localizer = localizer;
    }

    public async Task<MonitorLoadResult> LoadAsync(MonitorLoadContext context)
    {
        var now = DateTime.UtcNow;
        var from = context.TimeRange.GetFrom(now);
        var visibleServices = context.SelectedServiceId == MonitorLoadContext.AllServicesId
            ? context.AllServices
            : context.AllServices.Where(service => service.Id == context.SelectedServiceId).ToList();
        var visibleServiceIds = visibleServices.Select(service => service.Id).ToList();

        var visibleServiceItems = visibleServices
            .Select(service => new NamedItem(service.Id, service.Name))
            .ToList();

        var charts = await TimeseriesChartBuilder.BuildMonitorChartsAsync(
            _statsService,
            visibleServiceItems,
            context.SelectedClientIds,
            from,
            now,
            context.BucketCount,
            context.AllClients,
            _localizer);

        var recentFrom = now.Subtract(MonitorLoadContext.RecentWindow);
        var recentResponse = visibleServiceIds.Count == 0
            ? null
            : await _statsService.SearchTimeseriesAsync(new TimeseriesSearchRequest
            {
                SearchCategory = StatisticsSearchCategory.ServiceRequests,
                TargetIds = visibleServiceIds,
                ClientIds = TimeseriesChartBuilder.ResolveClientIds(
                    context.SelectedClientIds,
                    context.AllClients,
                    visibleServiceIds,
                    isService: true),
                FromUtc = recentFrom,
                ToUtc = now,
                BucketCount = 5
            });

        var rateLimits = await _rateLimitApi.GetByTargetTypeAsync(TargetType.Service);
        var rateLimitLookup = rateLimits.ToDictionary(limit => limit.TargetId);

        var rows = BuildClientRows(recentResponse, visibleServiceItems, rateLimitLookup, context.AllClients);
        var serviceStats = await BuildServiceStatsAsync(context, recentResponse, rateLimitLookup);

        return new MonitorLoadResult(charts, rows, serviceStats);
    }

    public bool TryRebuildFromCache(MonitorLoadContext context, out MonitorLoadResult result)
    {
        result = new MonitorLoadResult([], [], []);
        return false;
    }

    private static List<MonitorClientRow> BuildClientRows(
        TimeseriesSearchResponse? response,
        IReadOnlyList<NamedItem> visibleServices,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        IReadOnlyList<ClientConfiguration> allClients)
    {
        if (response is null)
        {
            return [];
        }

        var serviceNames = visibleServices.ToDictionary(service => service.Id, service => service.Name, StringComparer.Ordinal);
        var rows = new List<MonitorClientRow>();

        foreach (var target in response.Targets)
        {
            foreach (var clientSeries in target.ClientSeries)
            {
                var granted = clientSeries.Buckets.Sum(bucket => bucket.GrantedCount);
                var denied = clientSeries.Buckets.Sum(bucket =>
                    bucket.DeniedUnauthenticatedCount
                    + bucket.DeniedBlockedCount
                    + bucket.DeniedRateLimitedCount
                    + bucket.DeniedCapacityLimitedCount);

                if (granted <= 0 && denied <= 0)
                {
                    continue;
                }

                var cap = rateLimitLookup.TryGetValue(target.TargetId, out var limit)
                    ? MonitorCapCalculator.GetScaledGlobalServiceCap(target.TargetId, rateLimitLookup, MonitorLoadContext.RecentWindow)
                    : 0;

                rows.Add(new MonitorClientRow(
                    clientSeries.ClientId,
                    clientSeries.ClientName,
                    serviceNames.GetValueOrDefault(target.TargetId, target.TargetId),
                    granted,
                    denied,
                    clientSeries.Buckets.Sum(bucket => bucket.DeniedUnauthenticatedCount),
                    clientSeries.Buckets.Sum(bucket => bucket.DeniedBlockedCount),
                    clientSeries.Buckets.Sum(bucket => bucket.DeniedRateLimitedCount),
                    clientSeries.Buckets.Sum(bucket => bucket.DeniedCapacityLimitedCount),
                    cap));
            }
        }

        return rows;
    }

    private async Task<List<ServiceSummaryRow>> BuildServiceStatsAsync(
        MonitorLoadContext context,
        TimeseriesSearchResponse? recentResponse,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup)
    {
        var recentByService = recentResponse?.Targets.ToDictionary(target => target.TargetId, StringComparer.Ordinal)
            ?? new Dictionary<string, TimeseriesTargetSeries>(StringComparer.Ordinal);

        return context.AllServices.Select(service =>
        {
            recentByService.TryGetValue(service.Id, out var target);
            var entries = target?.ClientSeries ?? [];

            long granted = 0;
            long deniedUnauth = 0;
            long deniedBlocked = 0;
            long deniedRateLimited = 0;
            long deniedCapacity = 0;

            foreach (var clientSeries in entries)
            {
                granted += clientSeries.Buckets.Sum(bucket => bucket.GrantedCount);
                deniedUnauth += clientSeries.Buckets.Sum(bucket => bucket.DeniedUnauthenticatedCount);
                deniedBlocked += clientSeries.Buckets.Sum(bucket => bucket.DeniedBlockedCount);
                deniedRateLimited += clientSeries.Buckets.Sum(bucket => bucket.DeniedRateLimitedCount);
                deniedCapacity += clientSeries.Buckets.Sum(bucket => bucket.DeniedCapacityLimitedCount);
            }

            var breakdownEntries = entries.Select(client => new ClientManager.Shared.Models.Responses.ClientUsageEntry(
                client.ClientId,
                client.ClientName,
                client.Buckets.Sum(bucket => bucket.GrantedCount),
                client.Buckets.Sum(bucket => bucket.DeniedUnauthenticatedCount + bucket.DeniedBlockedCount + bucket.DeniedRateLimitedCount + bucket.DeniedCapacityLimitedCount),
                client.Buckets.Sum(bucket => bucket.DeniedUnauthenticatedCount),
                client.Buckets.Sum(bucket => bucket.DeniedBlockedCount),
                client.Buckets.Sum(bucket => bucket.DeniedRateLimitedCount),
                client.Buckets.Sum(bucket => bucket.DeniedCapacityLimitedCount),
                (long)client.Buckets.LastOrDefault()?.ActiveCount)).ToList();

            var (contributingUsage, offBudgetUsage) = MonitorCapCalculator.PartitionServiceUsage(
                breakdownEntries, service.Id, context.AllClients);
            var (cap, usesGlobalCap) = MonitorCapCalculator.GetServiceSummaryCap(
                service.Id, breakdownEntries, context.AllClients, rateLimitLookup, MonitorLoadContext.RecentWindow);

            return new ServiceSummaryRow(
                service.Id,
                service.Name,
                contributingUsage,
                offBudgetUsage,
                cap,
                usesGlobalCap,
                deniedUnauth + deniedBlocked + deniedRateLimited + deniedCapacity,
                deniedUnauth,
                deniedBlocked,
                deniedRateLimited,
                deniedCapacity);
        }).ToList();
    }
}
