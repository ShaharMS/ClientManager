using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class AllocationsSinglePoolChartLoader
{
    private readonly StatisticsApiService _statsService;

    public AllocationsSinglePoolChartLoader(StatisticsApiService statsService) => _statsService = statsService;

    public async Task<(List<TargetChartData> Charts, List<AllocationClientRow> Rows)> LoadAsync(
        AllocationsLoadContext context,
        List<ResourcePoolStatisticsResponse> visiblePools,
        IReadOnlyList<TargetClientUsageBreakdownResponse> breakdowns,
        IReadOnlyList<TargetClientUsageBreakdownResponse> recentBreakdowns,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        ChartBucketAggregator.AggregationMode chartAggregationMode,
        ChartBucketAggregator.AggregationResult chartTemplate,
        TimeSpan chartBucketDuration,
        DateTime from,
        DateTime now)
    {
        var charts = new List<TargetChartData>();
        var rows = new List<AllocationClientRow>();

        foreach (var pool in visiblePools)
        {
            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == pool.ResourcePoolId);
            var recentEntries = recentBreakdowns
                .FirstOrDefault(b => b.TargetId == pool.ResourcePoolId)?.Entries ?? [];
            var entries = breakdown?.Entries ?? [];
            var chartCap = AllocationsCapCalculator.GetPoolChartCap(
                pool, context.IsAccessMetric, rateLimitLookup, chartBucketDuration);

            var historiesByClientId = (await _statsService.GetHistoricalUsageByClientAsync(
                    "ResourcePool",
                    new[] { pool.ResourcePoolId },
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
                    .Select(point => new ChartBucketAggregator.RawPoint(
                        point.Timestamp,
                        AllocationsChartPointHelper.GetHistoricalPointValue(point, context.IsAccessMetric)))
                    .ToList();

                if (rawPoints.Count == 0)
                {
                    continue;
                }

                clientAggregations[entry.ClientId] = ChartBucketAggregator.Aggregate(
                    rawPoints, from, now, context.BucketCount, chartAggregationMode);
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
                    context, entry.ClientId, entry.ClientName, pool, recentEntry, rateLimitLookup));
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

            var poolHistory = (await _statsService.GetHistoricalUsageAsync(
                "ResourcePool", new[] { pool.ResourcePoolId }, null, from, now, context.TimeRange.Granularity))
                .FirstOrDefault();
            DeniedChartSeriesBuilder.AppendTripletSeries(
                clientAreas,
                pool.ResourcePoolId,
                pool.Name,
                poolHistory?.Points ?? [],
                context.IsAccessMetric ? DeniedViewMode.RateLimitDenied : DeniedViewMode.CapacityDenied,
                from,
                now,
                context.BucketCount);

            charts.Add(new TargetChartData(pool.Name, clientAreas, capPoints));
        }

        return (charts, rows);
    }
}
