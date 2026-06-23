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
    private readonly StatisticsApiService _statsService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public MonitorSingleServiceChartLoader(
        StatisticsApiService statsService,
        IStringLocalizer<SharedResources> localizer)
    {
        _statsService = statsService;
        _localizer = localizer;
    }

    public async Task LoadAsync(
        MonitorLoadContext context,
        List<Service> visibleServices,
        IReadOnlyList<TargetClientUsageBreakdownResponse> breakdowns,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        ChartBucketAggregator.AggregationResult chartTemplate,
        TimeSpan chartBucketDuration,
        TimeSpan rangeDuration,
        DateTime from,
        DateTime now,
        List<TargetChartData> charts,
        List<MonitorClientRow> rows)
    {
        foreach (var service in visibleServices)
        {
            var chartCap = MonitorCapCalculator.GetScaledGlobalServiceCap(
                service.Id, rateLimitLookup, chartBucketDuration);

            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == service.Id);
            var entries = breakdown?.Entries ?? [];

            var historiesByClientId = (await _statsService.GetHistoricalUsageByClientAsync(
                    "Service",
                    new[] { service.Id },
                    entries.Select(entry => entry.ClientId),
                    from,
                    now,
                    context.TimeRange.Granularity))
                .ToDictionary(history => history.ClientId);

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
                    rawPoints, from, now, context.BucketCount);
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

            var serviceHistory = (await _statsService.GetHistoricalUsageAsync(
                "Service", new[] { service.Id }, null, from, now, context.TimeRange.Granularity))
                .FirstOrDefault();
            DeniedChartSeriesBuilder.AppendTripletSeries(
                clientAreas,
                service.Id,
                serviceHistory?.Points ?? [],
                DeniedViewMode.RateLimitDenied,
                from,
                now,
                context.BucketCount,
                _localizer);

            charts.Add(new TargetChartData(service.Name, clientAreas, chartCapPoints));
        }
    }
}
