using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class DashboardAllTargetsChartLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly ResourcePoolApiService _poolService;
    private readonly GlobalRateLimitApiService _rateLimitApi;

    public DashboardAllTargetsChartLoader(
        StatisticsApiService statsService,
        ResourcePoolApiService poolService,
        GlobalRateLimitApiService rateLimitApi)
    {
        _statsService = statsService;
        _poolService = poolService;
        _rateLimitApi = rateLimitApi;
    }

    public async Task LoadAsync(DashboardChartLoadContext context, List<TargetChartData> charts)
    {
        var now = context.TimeRange.GetTo();
        var from = context.TimeRange.GetFrom(now);
        var isRateBased = context.SelectedFilterType == "Service";
        var chartAggregationMode = ChartValueHelper.GetAggregationMode(isRateBased);
        var emptyAggregation = ChartBucketAggregator.Aggregate([], from, now, mode: chartAggregationMode);

        var targets = (context.SelectedFilterType == "Service"
            ? context.AllServices
            : context.AllPools).Where(t => t.Id != DashboardChartLoadContext.AllTargetsId).ToList();

        var targetIds = targets.Select(t => t.Id).ToList();

        double totalBaseCap = 0;
        TimeSpan? rateWindow = null;

        if (isRateBased)
        {
            var rateLimits = await _rateLimitApi.GetByTargetTypeAsync(TargetType.Service);
            foreach (var target in targets)
            {
                var limit = rateLimits.FirstOrDefault(r => r.TargetId == target.Id);
                if (limit is not null)
                {
                    totalBaseCap += limit.MaxRequests;
                    rateWindow ??= limit.Window;
                }
            }
        }
        else
        {
            var pools = await _poolService.GetAllAsync();
            foreach (var target in targets)
            {
                var cap = pools.FirstOrDefault(p => p.Id == target.Id)?.MaxSlots ?? 0;
                totalBaseCap += cap;
            }
        }

        var histories = await _statsService.GetHistoricalUsageAsync(
            context.SelectedFilterType, targetIds, null, from, now, context.TimeRange.Granularity);

        var targetAggregations = histories
            .Select(history => ChartBucketAggregator.Aggregate(
                history.Points.Select(point => new ChartBucketAggregator.RawPoint(
                    point.Timestamp,
                    ChartValueHelper.GetHistoricalPointValue(point, isRateBased))),
                from,
                now,
                mode: chartAggregationMode))
            .ToList();

        var referenceBuckets = targetAggregations.FirstOrDefault()?.Buckets
            ?? emptyAggregation.Buckets;
        var sortedPoints = ChartValueHelper.SumBuckets(targetAggregations, referenceBuckets);

        var scaledCap = totalBaseCap;
        if (isRateBased && rateWindow.HasValue && rateWindow.Value > TimeSpan.Zero)
        {
            var scaleFactor = emptyAggregation.BucketDuration.TotalSeconds / rateWindow.Value.TotalSeconds;
            scaledCap = totalBaseCap * scaleFactor;
        }

        var capPoints = sortedPoints
            .Select(p => new ChartPoint(p.Label, scaledCap))
            .ToList();

        var label = context.SelectedFilterType == "Service" ? "All Services" : "All Resource Pools";
        charts.Add(new TargetChartData(label,
            new List<ClientAreaSeries> { new("total", "Total", sortedPoints) },
            capPoints));
    }
}
