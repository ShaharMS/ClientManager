using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class DashboardSingleTargetChartLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly ResourcePoolApiService _poolService;
    private readonly GlobalRateLimitApiService _rateLimitApi;
    private readonly IStringLocalizer<SharedResources> _localizer;

    private SingleTargetFetchCache? _cache;

    private sealed record SingleTargetFetchCache(
        string CacheKey,
        Dictionary<string, ClientHistoricalUsageResponse> HistoriesByClientId,
        HistoricalUsageResponse? TargetHistory,
        List<NamedItem> ClientsToQuery,
        double BaseCap,
        Dictionary<string, GlobalRateLimit> RateLimitLookup,
        string TargetName,
        bool IsRateBased,
        DateTime From,
        DateTime To,
        string Granularity);

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
        _cache = await FetchAsync(context);
        return BuildCharts(_cache, context, charts);
    }

    public bool TryRebuildFromCache(DashboardChartLoadContext context, List<TargetChartData> charts, out List<ClientUsagePoint> donutData)
    {
        if (_cache is null || _cache.CacheKey != BuildCacheKey(context))
        {
            donutData = [];
            return false;
        }

        donutData = BuildCharts(_cache, context, charts);
        return true;
    }

    private static string BuildCacheKey(DashboardChartLoadContext context) =>
        $"{context.SelectedFilterType}|{context.SelectedTargetId}|{string.Join(',', context.SelectedClientIds ?? [])}|{context.TimeRange.GetFrom():O}|{context.TimeRange.GetTo():O}|{context.TimeRange.Granularity}";

    private async Task<SingleTargetFetchCache> FetchAsync(DashboardChartLoadContext context)
    {
        var now = context.TimeRange.GetTo();
        var from = context.TimeRange.GetFrom(now);
        var isRateBased = context.SelectedFilterType == "Service";

        var clientsToQuery = context.SelectedClientIds?.Any() == true
            ? context.Clients.Where(c => context.SelectedClientIds.Contains(c.Id)).ToList()
            : context.Clients;

        double baseCap = 0;
        var rateLimitLookup = new Dictionary<string, GlobalRateLimit>(StringComparer.Ordinal);

        if (isRateBased)
        {
            var rateLimits = await _rateLimitApi.GetByTargetTypeAsync(TargetType.Service);
            var limit = rateLimits.FirstOrDefault(r => r.TargetId == context.SelectedTargetId);
            if (limit is not null)
            {
                baseCap = limit.MaxRequests;
                rateLimitLookup[limit.TargetId] = limit;
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

        var targetHistory = (await _statsService.GetHistoricalUsageAsync(
            context.SelectedFilterType,
            new[] { context.SelectedTargetId! },
            null,
            from,
            now,
            context.TimeRange.Granularity))
            .FirstOrDefault();

        var targetName = context.FilterTargets.FirstOrDefault(t => t.Id == context.SelectedTargetId)?.Name ?? "";

        return new SingleTargetFetchCache(
            BuildCacheKey(context),
            historiesByClientId,
            targetHistory,
            clientsToQuery,
            baseCap,
            rateLimitLookup,
            targetName,
            isRateBased,
            from,
            now,
            context.TimeRange.Granularity);
    }

    private List<ClientUsagePoint> BuildCharts(
        SingleTargetFetchCache cache,
        DashboardChartLoadContext context,
        List<TargetChartData> charts)
    {
        charts.Clear();

        var chartAggregationMode = ChartValueHelper.GetAggregationMode(cache.IsRateBased);
        var storageDuration = ChartGranularityHelper.GetStorageBucketDuration(cache.Granularity);
        var clientAggregations = new Dictionary<string, (NamedItem Client, ChartBucketAggregator.AggregationResult Result)>();
        var clientUsageValues = new Dictionary<string, double>();

        var emptyAggregation = ChartBucketAggregator.Aggregate([], cache.From, cache.To, context.BucketCount, chartAggregationMode, storageDuration);
        var bucketDuration = emptyAggregation.BucketDuration;

        foreach (var client in cache.ClientsToQuery)
        {
            cache.HistoriesByClientId.TryGetValue(client.Id, out var clientHistory);
            var rawPoints = (clientHistory?.Points ?? [])
                .Select(p => new ChartBucketAggregator.RawPoint(
                    p.Timestamp,
                    ChartValueHelper.GetHistoricalPointValue(p, cache.IsRateBased)))
                .ToList();

            if (rawPoints.Count == 0)
            {
                continue;
            }

            var aggregation = ChartBucketAggregator.Aggregate(
                rawPoints, cache.From, cache.To, context.BucketCount, chartAggregationMode, storageDuration);
            clientAggregations[client.Id] = (client, aggregation);
            clientUsageValues[client.Id] = ChartValueHelper.GetClientUsageValue(clientHistory?.Points ?? [], cache.IsRateBased);
        }

        var referenceBuckets = clientAggregations.Values.FirstOrDefault().Result?.Buckets
            ?? emptyAggregation.Buckets;

        var clientAreas = new List<ClientAreaSeries>();
        foreach (var (clientId, (client, agg)) in clientAggregations)
        {
            if (cache.IsRateBased
                && !MonitorCapCalculator.ContributesToGlobalServiceLimit(
                    clientId, context.SelectedTargetId!, context.AllClients))
            {
                continue;
            }

            var points = agg.Buckets
                .Select(b => new ChartPoint(b.Label, b.Value))
                .ToList();
            clientAreas.Add(new ClientAreaSeries(clientId, client.Name, points));
        }

        var entries = clientAggregations.Keys
            .Select(clientId => new ClientUsageEntry(
                clientId,
                clientAggregations[clientId].Client.Name,
                0, 0, 0, 0, 0, 0, 0))
            .ToList();

        var chartCap = cache.IsRateBased
            ? ChartCapResolver.ResolveServiceChartCap(
                context.SelectedTargetId!,
                entries,
                context.AllClients,
                cache.RateLimitLookup,
                bucketDuration)
            : ChartCapResolver.ResolvePoolSlotCap((int)cache.BaseCap);

        var capPoints = ChartCapResolver.BuildCapSeries(referenceBuckets, chartCap);

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

        DeniedChartSeriesBuilder.AppendTripletSeries(
            clientAreas,
            context.SelectedTargetId!,
            cache.TargetHistory?.Points ?? [],
            cache.IsRateBased ? DeniedViewMode.RateLimitDenied : DeniedViewMode.CapacityDenied,
            cache.From,
            cache.To,
            context.BucketCount,
            _localizer,
            storageDuration);

        if (cache.IsRateBased)
        {
            var aggregationByClientId = clientAggregations.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Result);
            var offBudgetPoints = OffBudgetChartSeriesBuilder.SumOffBudgetPoints(
                entries,
                context.SelectedTargetId!,
                context.AllClients,
                aggregationByClientId,
                referenceBuckets);
            OffBudgetChartSeriesBuilder.AppendSeries(
                clientAreas, context.SelectedTargetId!, offBudgetPoints, _localizer);
        }

        charts.Add(new TargetChartData(cache.TargetName, clientAreas, capPoints));

        return donutData;
    }
}
