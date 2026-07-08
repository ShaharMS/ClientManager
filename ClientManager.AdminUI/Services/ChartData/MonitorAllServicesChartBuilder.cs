using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class MonitorAllServicesChartBuilder
{
    internal static void Build(
        MonitorLoadContext context,
        List<Service> visibleServices,
        IReadOnlyList<TargetClientUsageBreakdownResponse> breakdowns,
        IReadOnlyList<HistoricalUsageResponse> allHistories,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        TimeSpan chartBucketDuration,
        TimeSpan rangeDuration,
        DateTime from,
        DateTime now,
        List<TargetChartData> charts,
        List<MonitorClientRow> rows,
        IStringLocalizer<SharedResources> localizer,
        TimeSpan storageBucketDuration)
    {
        var totalCap = 0;

        foreach (var service in visibleServices)
        {
            totalCap += MonitorCapCalculator.GetScaledGlobalServiceCap(
                service.Id, rateLimitLookup, chartBucketDuration);

            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == service.Id);

            foreach (var entry in breakdown?.Entries ?? [])
            {
                rows.Add(new MonitorClientRow(
                    entry.ClientId, entry.ClientName, service.Name,
                    entry.GrantedCount,
                    entry.DeniedCount,
                    entry.DeniedUnauthenticatedCount,
                    entry.DeniedBlockedCount,
                    entry.DeniedRateLimitedCount,
                    entry.DeniedCapacityLimitedCount,
                    MonitorCapCalculator.GetEffectiveClientServiceCap(
                        entry.ClientId, service.Id, context.AllClients, rateLimitLookup, rangeDuration)));
            }
        }

        var targetPointLists = visibleServices
            .Select(service => (IReadOnlyList<HistoricalUsagePoint>)(allHistories.FirstOrDefault(h => h.TargetId == service.Id)?.Points ?? []));
        var allServicesLabel = localizer["Pages.Monitor.Chart.AllServices"];
        var (clientAreas, referenceBuckets) = AggregateTargetChartSeriesBuilder.Build(
            targetPointLists,
            usageIsSummed: true,
            allServicesLabel,
            DeniedViewMode.RateLimitDenied,
            from,
            now,
            context.BucketCount,
            localizer,
            storageBucketDuration);

        var capPoints = referenceBuckets
            .Select(bucket => new ChartPoint(bucket.Label, totalCap))
            .ToList();

        charts.Add(new TargetChartData(allServicesLabel, clientAreas, capPoints));
    }
}
