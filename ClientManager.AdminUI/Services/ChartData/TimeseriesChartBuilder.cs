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

        var chart = BuildAggregateChart(
            response.Targets,
            context.SelectedFilterType == "Service",
            context.AllClients,
            localizer);
        return ([chart.Chart], chart.DonutSlices);
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

        if (response is null || response.Targets.Count == 0)
        {
            return [];
        }

        return [BuildAggregateChart(response.Targets, isRateBased: true, allClients, localizer).Chart];
    }

    public static (TargetChartData Chart, List<ClientUsagePoint> DonutSlices) BuildAggregateChart(
        IReadOnlyList<TimeseriesTargetSeries> targets,
        bool isRateBased,
        IReadOnlyList<ClientConfiguration> allClients,
        IStringLocalizer<SharedResources> localizer)
    {
        var clientEntries = targets
            .SelectMany(target => target.ClientSeries.Select(series => (Target: target, Series: series)))
            .ToList();
        var valueSelector = isRateBased
            ? (Func<TimeseriesDisplayBucket, double>)(bucket => bucket.GrantedCount)
            : bucket => bucket.ActiveCount;
        var contributingEntries = clientEntries
            .Where(entry => !isRateBased
                || MonitorCapCalculator.ContributesToGlobalServiceLimit(
                    entry.Series.ClientId, entry.Target.TargetId, allClients))
            .ToList();
        var clientAreas = new List<ClientAreaSeries>();
        var aggregatePoints = AggregateDisplayBuckets(
            contributingEntries.SelectMany(entry => entry.Series.Buckets),
            valueSelector);
        var aggregateName = targets.Count == 1 ? targets[0].TargetName : string.Empty;

        if (aggregatePoints.Any(point => point.Value > 0))
        {
            clientAreas.Add(new ClientAreaSeries(
                ChartAggregator.AggregateSeriesId,
                aggregateName,
                aggregatePoints));
        }

        if (isRateBased)
        {
            var offBudgetPoints = AggregateDisplayBuckets(
                clientEntries
                    .Where(entry => !MonitorCapCalculator.ContributesToGlobalServiceLimit(
                        entry.Series.ClientId, entry.Target.TargetId, allClients))
                    .SelectMany(entry => entry.Series.Buckets),
                valueSelector);
            OffBudgetChartSeriesBuilder.AppendSeries(
                clientAreas,
                ChartAggregator.AggregateSeriesId,
                offBudgetPoints,
                localizer);
        }

        AppendAggregateDeniedSeries(clientAreas, targets, isRateBased, localizer);

        var cap = targets.All(target => target.CapValue > 0)
            ? (int)targets.Sum(target => target.CapValue)
            : 0;
        var capPoints = ChartCapResolver.BuildCapSeries(
            BuildReferenceBuckets(targets),
            cap);
        var donutSlices = contributingEntries
            .Select(entry => new ClientUsagePoint(
                entry.Series.ClientId,
                entry.Series.ClientName,
                entry.Series.Buckets.Sum(valueSelector)))
            .Where(slice => slice.Value > 0)
            .ToList();

        return (new TargetChartData(aggregateName, clientAreas, capPoints), donutSlices);
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

    private static void AppendAggregateDeniedSeries(
        ICollection<ClientAreaSeries> series,
        IReadOnlyList<TimeseriesTargetSeries> targets,
        bool isRateBased,
        IStringLocalizer<SharedResources> localizer)
    {
        var buckets = targets.SelectMany(target => target.AggregateBuckets).ToList();
        foreach (var (suffix, label) in GetDeniedDefinitions(isRateBased, localizer))
        {
            var points = AggregateDisplayBuckets(
                buckets,
                bucket => suffix switch
                {
                    ChartAggregator.DeniedUnauthSuffix => bucket.DeniedUnauthenticatedCount,
                    ChartAggregator.DeniedBlockedSuffix => bucket.DeniedBlockedCount,
                    ChartAggregator.DeniedRateLimitedSuffix => bucket.DeniedRateLimitedCount,
                    ChartAggregator.DeniedCapacitySuffix => bucket.DeniedCapacityLimitedCount,
                    _ => 0
                });

            series.Add(new ClientAreaSeries(
                ChartAggregator.AggregateSeriesId + suffix,
                label,
                points,
                Hidden: true));
        }
    }

    internal static List<ChartPoint> AggregateDisplayBuckets(
        IEnumerable<TimeseriesDisplayBucket> buckets,
        Func<TimeseriesDisplayBucket, double> valueSelector) =>
        buckets
            .GroupBy(bucket => (bucket.Label, bucket.BucketStartUtc, bucket.BucketEndUtc))
            .OrderBy(group => group.Key.BucketStartUtc)
            .Select(group => new ChartPoint(
                group.Key.Label,
                group.Sum(valueSelector)))
            .ToList();

    private static List<ChartBucketAggregator.AggregatedBucket> BuildReferenceBuckets(
        IReadOnlyList<TimeseriesTargetSeries> targets) =>
        targets
            .SelectMany(target => target.AggregateBuckets)
            .GroupBy(bucket => (bucket.Label, bucket.BucketStartUtc, bucket.BucketEndUtc))
            .OrderBy(group => group.Key.BucketStartUtc)
            .Select(group => new ChartBucketAggregator.AggregatedBucket(
                group.Key.Label,
                0,
                group.Key.BucketStartUtc,
                group.Key.BucketEndUtc))
            .ToList();

    private static (string Suffix, string Label)[] GetDeniedDefinitions(
        bool isRateBased,
        IStringLocalizer<SharedResources> localizer) =>
        isRateBased
            ?
            [
                (ChartAggregator.DeniedUnauthSuffix, localizer[TermKeys.DeniedUnauthenticated]),
                (ChartAggregator.DeniedBlockedSuffix, localizer[TermKeys.DeniedBlocked]),
                (ChartAggregator.DeniedRateLimitedSuffix, localizer[TermKeys.DeniedThrottled])
            ]
            :
            [
                (ChartAggregator.DeniedUnauthSuffix, localizer[TermKeys.DeniedUnauthenticated]),
                (ChartAggregator.DeniedBlockedSuffix, localizer[TermKeys.DeniedBlocked]),
                (ChartAggregator.DeniedCapacitySuffix, localizer[TermKeys.DeniedOutOfSlots])
            ];
}
