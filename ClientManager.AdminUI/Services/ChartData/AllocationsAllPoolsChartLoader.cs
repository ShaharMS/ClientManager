using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class AllocationsAllPoolsChartLoader
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AllocationsAllPoolsChartLoader(
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
        var totalMaxSlots = 0;

        foreach (var pool in cache.VisiblePools)
        {
            totalMaxSlots += AllocationsCapCalculator.GetPoolChartCap(
                pool, context.IsAccessMetric, cache.RateLimitLookup, chartBucketDuration);

            var breakdown = cache.Breakdowns.FirstOrDefault(b => b.TargetId == pool.ResourcePoolId);
            var recentEntries = cache.RecentBreakdowns
                .FirstOrDefault(b => b.TargetId == pool.ResourcePoolId)?.Entries ?? [];

            foreach (var entry in breakdown?.Entries ?? [])
            {
                var recentEntry = recentEntries.FirstOrDefault(e => e.ClientId == entry.ClientId);
                rows.Add(AllocationsClientRowFactory.Create(
                    context, entry.ClientId, entry.ClientName, pool, recentEntry, cache.RateLimitLookup));
            }
        }

        var allPoolsLabel = _localizer["Pages.Allocations.Target.AllPools"];
        var targetPointLists = cache.VisiblePools
            .Select(pool => (IReadOnlyList<HistoricalUsagePoint>)(cache.AllHistories
                .FirstOrDefault(h => h.TargetId == pool.ResourcePoolId)?.Points ?? []));
        var (clientAreas, referenceBuckets) = AggregateTargetChartSeriesBuilder.Build(
            targetPointLists,
            context.IsAccessMetric,
            allPoolsLabel,
            context.IsAccessMetric ? DeniedViewMode.RateLimitDenied : DeniedViewMode.CapacityDenied,
            cache.From,
            cache.Now,
            context.BucketCount,
            _localizer,
            storageDuration);

        var capPoints = referenceBuckets
            .Select(bucket => new ChartPoint(bucket.Label, totalMaxSlots))
            .ToList();
        charts.Add(new TargetChartData(allPoolsLabel, clientAreas, capPoints));

        return (charts, rows);
    }
}
