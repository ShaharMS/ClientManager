using ClientManager.AdminUI.Localization;
using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

/// <summary>
/// Maps chart-ready statistics search responses into dashboard chart models.
/// </summary>
public static class TimeseriesChartBuilder
{
    public static async Task<TimeseriesSearchResponse?> FetchDashboardAsync(
        StatisticsApiService statsService,
        DashboardChartLoadContext context)
    {
        var now = context.TimeRange.GetTo();
        var from = context.TimeRange.GetFrom(now);
        var isService = context.SelectedFilterType == "Service";
        var targetIds = ResolveTargetIds(context);

        return await statsService.SearchTimeseriesAsync(new TimeseriesSearchRequest
        {
            SearchCategory = isService
                ? StatisticsSearchCategory.ServiceRequests
                : StatisticsSearchCategory.ResourcePoolAllocations,
            TargetIds = targetIds,
            ClientIds = ResolveClientIds(context.SelectedClientIds, context.AllClients, targetIds, isService),
            FromUtc = from,
            ToUtc = now,
            BucketCount = context.BucketCount
        });
    }

    public static (List<TargetChartData> Charts, List<ClientUsagePoint> DonutSlices) BuildDashboardFromResponse(
        TimeseriesSearchResponse? response,
        DashboardChartLoadContext context,
        IStringLocalizer<SharedResources> localizer)
    {
        if (response is null || response.Targets.Count == 0)
        {
            return ([], []);
        }

        var isService = context.SelectedFilterType == "Service";
        var charts = new List<TargetChartData>();
        var donutSlices = new List<ClientUsagePoint>();

        foreach (var target in response.Targets)
        {
            var chart = BuildTargetChart(
                target,
                isService,
                context.AllClients,
                localizer);
            charts.Add(chart.Chart);
            donutSlices.AddRange(chart.DonutSlices);
        }

        return (charts, donutSlices);
    }

    public static async Task<(List<TargetChartData> Charts, List<ClientUsagePoint> DonutSlices)> BuildDashboardAsync(
        StatisticsApiService statsService,
        DashboardChartLoadContext context,
        IStringLocalizer<SharedResources> localizer)
    {
        var response = await FetchDashboardAsync(statsService, context);
        return BuildDashboardFromResponse(response, context, localizer);
    }

    public static IReadOnlyList<string> ResolveClientIds(
        IEnumerable<string>? selectedClientIds,
        IReadOnlyList<ClientConfiguration> allClients,
        IReadOnlyList<string> targetIds,
        bool isService)
    {
        if (selectedClientIds?.Any() == true)
        {
            return selectedClientIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        var targetIdSet = targetIds.ToHashSet(StringComparer.Ordinal);
        return allClients
            .Where(client => isService
                ? client.Services.Keys.Any(targetIdSet.Contains)
                : client.ResourcePools.Keys.Any(targetIdSet.Contains))
            .Select(client => client.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    public static async Task<List<TargetChartData>> BuildMonitorChartsAsync(
        StatisticsApiService statsService,
        IReadOnlyList<NamedItem> visibleServices,
        IEnumerable<string>? selectedClientIds,
        DateTime from,
        DateTime to,
        int bucketCount,
        IReadOnlyList<ClientConfiguration> allClients,
        IStringLocalizer<SharedResources> localizer)
    {
        var targetIds = visibleServices.Select(service => service.Id).ToList();
        var response = await statsService.SearchTimeseriesAsync(new TimeseriesSearchRequest
        {
            SearchCategory = StatisticsSearchCategory.ServiceRequests,
            TargetIds = targetIds,
            ClientIds = ResolveClientIds(selectedClientIds, allClients, targetIds, isService: true),
            FromUtc = from,
            ToUtc = to,
            BucketCount = bucketCount
        });

        if (response is null)
        {
            return [];
        }

        return response.Targets
            .Select(target => BuildTargetChart(target, isRateBased: true, allClients, localizer).Chart)
            .ToList();
    }

    public static async Task<TimeseriesSearchResponse?> FetchAllocationsAsync(
        StatisticsApiService statsService,
        IEnumerable<string> poolIds,
        IEnumerable<string>? selectedClientIds,
        IReadOnlyList<ClientConfiguration> allClients,
        DateTime from,
        DateTime to,
        int bucketCount,
        bool isAccessMetric)
    {
        var targetIds = poolIds.ToList();
        return await statsService.SearchTimeseriesAsync(new TimeseriesSearchRequest
        {
            SearchCategory = isAccessMetric
                ? StatisticsSearchCategory.ResourcePoolRequests
                : StatisticsSearchCategory.ResourcePoolAllocations,
            TargetIds = targetIds,
            ClientIds = ResolveClientIds(selectedClientIds, allClients, targetIds, isService: false),
            FromUtc = from,
            ToUtc = to,
            BucketCount = bucketCount
        });
    }

    private static IReadOnlyList<string> ResolveTargetIds(DashboardChartLoadContext context)
    {
        if (context.SelectedTargetId != DashboardChartLoadContext.AllTargetsId)
        {
            return [context.SelectedTargetId!];
        }

        var items = context.SelectedFilterType == "Service" ? context.AllServices : context.AllPools;
        return items
            .Where(item => item.Id != DashboardChartLoadContext.AllTargetsId)
            .Select(item => item.Id)
            .ToList();
    }

    public static (TargetChartData Chart, List<ClientUsagePoint> DonutSlices) BuildTargetChart(
        TimeseriesTargetSeries target,
        bool isRateBased,
        IReadOnlyList<ClientConfiguration> allClients,
        IStringLocalizer<SharedResources> localizer)
    {
        var clientAreas = new List<ClientAreaSeries>();
        var donutSlices = new List<ClientUsagePoint>();

        foreach (var clientSeries in target.ClientSeries)
        {
            if (isRateBased
                && !MonitorCapCalculator.ContributesToGlobalServiceLimit(clientSeries.ClientId, target.TargetId, allClients))
            {
                continue;
            }

            var valueSelector = isRateBased
                ? (Func<TimeseriesDisplayBucket, double>)(bucket => bucket.GrantedCount)
                : bucket => bucket.ActiveCount;

            var points = clientSeries.Buckets
                .Select(bucket => new ChartPoint(bucket.Label, valueSelector(bucket)))
                .ToList();

            if (points.All(point => point.Value <= 0))
            {
                continue;
            }

            clientAreas.Add(new ClientAreaSeries(clientSeries.ClientId, clientSeries.ClientName, points));
            donutSlices.Add(new ClientUsagePoint(
                clientSeries.ClientId,
                clientSeries.ClientName,
                clientSeries.Buckets.Sum(bucket => valueSelector(bucket))));
        }

        var aggregatedAreas = ChartAggregator.Aggregate(
            clientAreas.Select(area => new ChartAggregator.AggregatedSeries(
                area.ClientId,
                area.ClientName,
                area.Points.Select(point => new ChartAggregator.AggregatedPoint(point.Label, point.Value)).ToList()))
            .ToList());

        clientAreas = aggregatedAreas
            .Select(area => new ClientAreaSeries(
                area.Id,
                area.Name,
                area.Points.Select(point => new ChartPoint(point.Label, point.Value)).ToList()))
            .ToList();

        AppendDeniedSeries(clientAreas, target, isRateBased, localizer);

        var referenceBuckets = target.ClientSeries.FirstOrDefault()?.Buckets ?? target.AggregateBuckets;
        var cap = isRateBased
            ? (int)target.CapValue
            : ChartCapResolver.ResolvePoolSlotCap((int)target.CapValue);
        var capPoints = ChartCapResolver.BuildCapSeries(
            referenceBuckets.Select(bucket => new ChartBucketAggregator.AggregatedBucket(
                bucket.Label, cap, bucket.BucketStartUtc, bucket.BucketEndUtc)).ToList(),
            cap);

        return (new TargetChartData(target.TargetName, clientAreas, capPoints), donutSlices);
    }

    private static void AppendDeniedSeries(
        ICollection<ClientAreaSeries> series,
        TimeseriesTargetSeries target,
        bool isRateBased,
        IStringLocalizer<SharedResources> localizer)
    {
        var mode = isRateBased ? DeniedViewMode.RateLimitDenied : DeniedViewMode.CapacityDenied;
        (string Suffix, string Label)[] definitions = mode == DeniedViewMode.CapacityDenied
            ?
            [
                (ChartAggregator.DeniedUnauthSuffix, localizer[TermKeys.DeniedUnauthenticated]),
                (ChartAggregator.DeniedBlockedSuffix, localizer[TermKeys.DeniedBlocked]),
                (ChartAggregator.DeniedCapacitySuffix, localizer[TermKeys.DeniedOutOfSlots])
            ]
            :
            [
                (ChartAggregator.DeniedUnauthSuffix, localizer[TermKeys.DeniedUnauthenticated]),
                (ChartAggregator.DeniedBlockedSuffix, localizer[TermKeys.DeniedBlocked]),
                (ChartAggregator.DeniedRateLimitedSuffix, localizer[TermKeys.DeniedThrottled])
            ];

        foreach (var (suffix, label) in definitions)
        {
            var points = target.AggregateBuckets
                .Select(bucket => new ChartPoint(
                    bucket.Label,
                    suffix switch
                    {
                        ChartAggregator.DeniedUnauthSuffix => bucket.DeniedUnauthenticatedCount,
                        ChartAggregator.DeniedBlockedSuffix => bucket.DeniedBlockedCount,
                        ChartAggregator.DeniedRateLimitedSuffix => bucket.DeniedRateLimitedCount,
                        ChartAggregator.DeniedCapacitySuffix => bucket.DeniedCapacityLimitedCount,
                        _ => 0
                    }))
                .ToList();

            series.Add(new ClientAreaSeries(target.TargetId + suffix, label, points, Hidden: true));
        }
    }
}
