using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Enums;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class DashboardSingleTargetChartLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly ResourcePoolApiService _poolService;
    private readonly GlobalRateLimitApiService _rateLimitApi;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DashboardSingleTargetChartLoader(
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

    public async Task<List<ClientUsagePoint>> LoadAsync(
        DashboardChartLoadContext context,
        List<TargetChartData> charts)
    {
        var now = context.TimeRange.GetTo();
        var from = context.TimeRange.GetFrom(now);
        var isRateBased = context.SelectedFilterType == "Service";
        var chartAggregationMode = ChartValueHelper.GetAggregationMode(isRateBased);

        var clientsToQuery = context.SelectedClientIds?.Any() == true
            ? context.Clients.Where(c => context.SelectedClientIds.Contains(c.Id)).ToList()
            : context.Clients;

        double baseCap = 0;
        TimeSpan? rateWindow = null;

        if (isRateBased)
        {
            var rateLimits = await _rateLimitApi.GetByTargetTypeAsync(TargetType.Service);
            var limit = rateLimits.FirstOrDefault(r => r.TargetId == context.SelectedTargetId);
            if (limit is not null)
            {
                baseCap = limit.MaxRequests;
                rateWindow = limit.Window;
            }
        }
        else
        {
            var pools = await _poolService.GetAllAsync();
            baseCap = pools.FirstOrDefault(p => p.Id == context.SelectedTargetId)?.MaxSlots ?? 0;
        }

        var historiesByClientId = (await _statsService.GetHistoricalUsageByClientAsync(
                context.SelectedFilterType,
                new[] { context.SelectedTargetId! },
                clientsToQuery.Select(client => client.Id),
                from,
                now,
                context.TimeRange.Granularity))
            .ToDictionary(history => history.ClientId);

        var clientAggregations = new Dictionary<string, (NamedItem Client, ChartBucketAggregator.AggregationResult Result)>();
        var clientUsageValues = new Dictionary<string, double>();

        var emptyAggregation = ChartBucketAggregator.Aggregate([], from, now, context.BucketCount, chartAggregationMode);
        var bucketDuration = emptyAggregation.BucketDuration;

        foreach (var client in clientsToQuery)
        {
            historiesByClientId.TryGetValue(client.Id, out var clientHistory);
            var rawPoints = (clientHistory?.Points ?? [])
                .Select(p => new ChartBucketAggregator.RawPoint(
                    p.Timestamp,
                    ChartValueHelper.GetHistoricalPointValue(p, isRateBased)))
                .ToList();

            if (rawPoints.Count == 0)
            {
                continue;
            }

            var aggregation = ChartBucketAggregator.Aggregate(
                rawPoints, from, now, context.BucketCount, chartAggregationMode);
            clientAggregations[client.Id] = (client, aggregation);
            clientUsageValues[client.Id] = ChartValueHelper.GetClientUsageValue(clientHistory?.Points ?? [], isRateBased);
        }

        var referenceBuckets = clientAggregations.Values.FirstOrDefault().Result?.Buckets
            ?? emptyAggregation.Buckets;
        var bucketLabels = referenceBuckets.Select(b => b.Label).ToList();

        var clientAreas = new List<ClientAreaSeries>();
        foreach (var (clientId, (client, agg)) in clientAggregations)
        {
            var points = agg.Buckets
                .Select(b => new ChartPoint(b.Label, b.Value))
                .ToList();
            clientAreas.Add(new ClientAreaSeries(clientId, client.Name, points));
        }

        var scaledCap = baseCap;
        if (isRateBased && rateWindow.HasValue && rateWindow.Value > TimeSpan.Zero && bucketDuration > TimeSpan.Zero)
        {
            var scaleFactor = bucketDuration.TotalSeconds / rateWindow.Value.TotalSeconds;
            scaledCap = baseCap * scaleFactor;
        }

        var capPoints = bucketLabels
            .Select(label => new ChartPoint(label, scaledCap))
            .ToList();

        var targetName = context.FilterTargets.FirstOrDefault(t => t.Id == context.SelectedTargetId)?.Name ?? "";

        var donutData = new List<ClientUsagePoint>();
        foreach (var (clientId, (client, _)) in clientAggregations)
        {
            var totalValue = clientUsageValues.GetValueOrDefault(clientId);
            if (totalValue > 0)
            {
                donutData.Add(new ClientUsagePoint(clientId, client.Name, totalValue));
            }
        }

        var aggregatedAreas = ChartAggregator.Aggregate(
            clientAreas.Select(c => new ChartAggregator.AggregatedSeries(
                c.ClientId, c.ClientName,
                c.Points.Select(p => new ChartAggregator.AggregatedPoint(p.Label, p.Value)).ToList()
            )).ToList());

        clientAreas = aggregatedAreas.Select(a => new ClientAreaSeries(
            a.Id, a.Name,
            a.Points.Select(p => new ChartPoint(p.Label, p.Value)).ToList()
        )).ToList();

        var targetHistory = (await _statsService.GetHistoricalUsageAsync(
            context.SelectedFilterType,
            new[] { context.SelectedTargetId! },
            null,
            from,
            now,
            context.TimeRange.Granularity))
            .FirstOrDefault();
        DeniedChartSeriesBuilder.AppendTripletSeries(
            clientAreas,
            context.SelectedTargetId!,
            targetHistory?.Points ?? [],
            isRateBased ? DeniedViewMode.RateLimitDenied : DeniedViewMode.CapacityDenied,
            from,
            now,
            context.BucketCount,
            _localizer);

        charts.Add(new TargetChartData(targetName, clientAreas, capPoints));

        return donutData;
    }
}
