using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class AllocationsSinglePoolChartLoader
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AllocationsSinglePoolChartLoader(
        StatisticsApiService statsService,
        IStringLocalizer<SharedResources> localizer)
    {
        _ = statsService;
        _localizer = localizer;
    }

    public (List<TargetChartData> Charts, List<AllocationClientRow> Rows) BuildFromCache(
        AllocationsLoadContext context,
        AllocationsFetchCache cache,
        ChartBucketAggregator.AggregationMode chartAggregationMode,
        ChartBucketAggregator.AggregationResult chartTemplate,
        TimeSpan chartBucketDuration,
        TimeSpan storageDuration)
    {
        var charts = new List<TargetChartData>();
        var rows = new List<AllocationClientRow>();

        foreach (var pool in cache.VisiblePools)
        {
            var breakdown = cache.Breakdowns.FirstOrDefault(b => b.TargetId == pool.ResourcePoolId);
            var recentEntries = cache.RecentBreakdowns
                .FirstOrDefault(b => b.TargetId == pool.ResourcePoolId)?.Entries ?? [];
            var entries = breakdown?.Entries ?? [];
            var chartCap = AllocationsCapCalculator.GetPoolChartCap(
                pool, context.IsAccessMetric, cache.RateLimitLookup, chartBucketDuration);

            cache.ClientHistoriesByPool.TryGetValue(pool.ResourcePoolId, out var historiesByClientId);
            historiesByClientId ??= [];

            var clientAreas = new List<ClientAreaSeries>();
            var clientAggregations = new Dictionary<string, ChartBucketAggregator.AggregationResult>();

            foreach (var entry in entries)
            {
                historiesByClientId.TryGetValue(entry.ClientId, out var historyData);
                var rawPoints = (historyData?.Points ?? [])
                    .Select(point => new ChartBucketAggregator.RawPoint(
                        point.Timestamp,
                        AllocationsChartPointHelper.GetHistoricalPointValue(point, context.IsAccessMetric)))
                    .ToList();

                if (rawPoints.Count == 0)
                {
                    continue;
                }

                clientAggregations[entry.ClientId] = ChartBucketAggregator.Aggregate(
                    rawPoints, cache.From, cache.Now, context.BucketCount, chartAggregationMode, storageDuration);
            }

            var referenceBuckets = clientAggregations.Values.FirstOrDefault()?.Buckets ?? chartTemplate.Buckets;

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

                var recentEntry = recentEntries.FirstOrDefault(e => e.ClientId == entry.ClientId);
                rows.Add(AllocationsClientRowFactory.Create(
                    context, entry.ClientId, entry.ClientName, pool, recentEntry, cache.RateLimitLookup));
            }

            var capPoints = referenceBuckets
                .Select(bucket => new ChartPoint(bucket.Label, chartCap))
                .ToList();

            var aggregatedAreas = ChartAggregator.Aggregate(
                clientAreas.Select(c => new ChartAggregator.AggregatedSeries(
                    c.ClientId, c.ClientName,
                    c.Points.Select(p => new ChartAggregator.AggregatedPoint(p.Label, p.Value)).ToList()
                )).ToList());

            clientAreas = aggregatedAreas.Select(a => new ClientAreaSeries(
                a.Id, a.Name,
                a.Points.Select(p => new ChartPoint(p.Label, p.Value)).ToList()
            )).ToList();

            cache.PoolHistories.TryGetValue(pool.ResourcePoolId, out var poolHistory);
            DeniedChartSeriesBuilder.AppendTripletSeries(
                clientAreas,
                pool.ResourcePoolId,
                poolHistory?.Points ?? [],
                context.IsAccessMetric ? DeniedViewMode.RateLimitDenied : DeniedViewMode.CapacityDenied,
                cache.From,
                cache.Now,
                context.BucketCount,
                _localizer,
                storageDuration);

            charts.Add(new TargetChartData(pool.Name, clientAreas, capPoints));
        }

        return (charts, rows);
    }
}
