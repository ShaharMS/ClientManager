using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class MonitorSingleServiceChartLoader
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public MonitorSingleServiceChartLoader(
        StatisticsApiService statsService,
        IStringLocalizer<SharedResources> localizer)
    {
        _ = statsService;
        _localizer = localizer;
    }

    public void BuildFromCache(
        MonitorLoadContext context,
        MonitorFetchCache cache,
        ChartBucketAggregator.AggregationResult chartTemplate,
        TimeSpan chartBucketDuration,
        List<TargetChartData> charts,
        List<MonitorClientRow> rows,
        TimeSpan storageDuration)
    {
        BuildCharts(
            context,
            cache.VisibleServices,
            cache.Breakdowns,
            cache.RateLimitLookup,
            cache.ClientHistoriesByService,
            cache.ServiceHistories,
            chartTemplate,
            chartBucketDuration,
            cache.RangeDuration,
            cache.From,
            cache.Now,
            charts,
            rows,
            storageDuration);
    }

    private void BuildCharts(
        MonitorLoadContext context,
        List<Service> visibleServices,
        IReadOnlyList<TargetClientUsageBreakdownResponse> breakdowns,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        Dictionary<string, Dictionary<string, ClientHistoricalUsageResponse>> historiesByService,
        Dictionary<string, HistoricalUsageResponse?> serviceHistories,
        ChartBucketAggregator.AggregationResult chartTemplate,
        TimeSpan chartBucketDuration,
        TimeSpan rangeDuration,
        DateTime from,
        DateTime now,
        List<TargetChartData> charts,
        List<MonitorClientRow> rows,
        TimeSpan storageDuration)
    {
        charts.Clear();
        rows.Clear();

        foreach (var service in visibleServices)
        {
            var chartCap = MonitorCapCalculator.GetScaledGlobalServiceCap(
                service.Id, rateLimitLookup, chartBucketDuration);

            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == service.Id);
            var entries = breakdown?.Entries ?? [];

            historiesByService.TryGetValue(service.Id, out var historiesByClientId);
            historiesByClientId ??= [];

            var clientAreas = new List<ClientAreaSeries>();
            var clientAggregations = new Dictionary<string, ChartBucketAggregator.AggregationResult>();

            foreach (var entry in entries)
            {
                historiesByClientId.TryGetValue(entry.ClientId, out var historyData);
                var rawPoints = (historyData?.Points ?? [])
                    .Select(point => new ChartBucketAggregator.RawPoint(point.Timestamp, point.GrantedCount))
                    .ToList();

                if (rawPoints.Count == 0)
                {
                    continue;
                }

                clientAggregations[entry.ClientId] = ChartBucketAggregator.Aggregate(
                    rawPoints, from, now, context.BucketCount, ChartBucketAggregator.AggregationMode.Sum, storageDuration);
            }

            var referenceBuckets = clientAggregations.Values.FirstOrDefault()?.Buckets
                ?? chartTemplate.Buckets;

            foreach (var entry in entries)
            {
                if (!clientAggregations.TryGetValue(entry.ClientId, out var aggregation))
                {
                    continue;
                }

                var points = aggregation.Buckets
                    .Select(bucket => new ChartPoint(bucket.Label, bucket.Value))
                    .ToList();

                clientAreas.Add(new ClientAreaSeries(entry.ClientId, entry.ClientName, points));

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

            var chartCapPoints = referenceBuckets
                .Select(bucket => new ChartPoint(bucket.Label, chartCap))
                .ToList();

            var aggregated = ChartAggregator.Aggregate(
                clientAreas.Select(c => new ChartAggregator.AggregatedSeries(
                    c.ClientId, c.ClientName,
                    c.Points.Select(p => new ChartAggregator.AggregatedPoint(p.Label, p.Value)).ToList()
                )).ToList());

            clientAreas = aggregated.Select(a => new ClientAreaSeries(
                a.Id, a.Name,
                a.Points.Select(p => new ChartPoint(p.Label, p.Value)).ToList()
            )).ToList();

            serviceHistories.TryGetValue(service.Id, out var serviceHistory);
            DeniedChartSeriesBuilder.AppendTripletSeries(
                clientAreas,
                service.Id,
                serviceHistory?.Points ?? [],
                DeniedViewMode.RateLimitDenied,
                from,
                now,
                context.BucketCount,
                _localizer,
                context.ShowDeniedBreakdown,
                storageDuration);

            charts.Add(new TargetChartData(service.Name, clientAreas, chartCapPoints));
        }
    }
}
