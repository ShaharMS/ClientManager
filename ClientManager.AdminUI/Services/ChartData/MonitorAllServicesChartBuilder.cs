using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

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
        List<MonitorClientRow> rows)
    {
        var rawPoints = new List<ChartBucketAggregator.RawPoint>();
        var totalCap = 0;

        foreach (var service in visibleServices)
        {
            totalCap += MonitorCapCalculator.GetScaledGlobalServiceCap(
                service.Id, rateLimitLookup, chartBucketDuration);

            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == service.Id);
            var history = allHistories.FirstOrDefault(h => h.TargetId == service.Id);

            rawPoints.AddRange((history?.Points ?? [])
                .Select(point => new ChartBucketAggregator.RawPoint(point.Timestamp, point.GrantedCount)));

            foreach (var entry in breakdown?.Entries ?? [])
            {
                rows.Add(new MonitorClientRow(
                    entry.ClientId, entry.ClientName, service.Name,
                    entry.GrantedCount,
                    entry.DeniedCount,
                    MonitorCapCalculator.GetEffectiveClientServiceCap(
                        entry.ClientId, service.Id, context.AllClients, rateLimitLookup, rangeDuration)));
            }
        }

        var aggregation = ChartBucketAggregator.Aggregate(rawPoints, from, now, context.BucketCount);
        var sortedPoints = aggregation.Buckets
            .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
            .ToList();
        var capPoints = sortedPoints
            .Select(p => new ChartPoint(p.Label, totalCap))
            .ToList();

        charts.Add(new TargetChartData("All Services",
            new List<ClientAreaSeries> { new("total", "Total", sortedPoints) },
            capPoints));
    }
}
