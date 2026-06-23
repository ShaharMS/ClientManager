using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class DashboardAllTargetsChartLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly ResourcePoolApiService _poolService;
    private readonly GlobalRateLimitApiService _rateLimitApi;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DashboardAllTargetsChartLoader(
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

    public async Task LoadAsync(DashboardChartLoadContext context, List<TargetChartData> charts)
    {
        var now = context.TimeRange.GetTo();
        var from = context.TimeRange.GetFrom(now);
        var isRateBased = context.SelectedFilterType == "Service";
        var chartAggregationMode = ChartValueHelper.GetAggregationMode(isRateBased);
        var emptyAggregation = ChartBucketAggregator.Aggregate([], from, now, context.BucketCount, chartAggregationMode);

        var targets = (context.SelectedFilterType == "Service"
            ? context.AllServices
            : context.AllPools).Where(t => t.Id != DashboardChartLoadContext.AllTargetsId).ToList();

        var targetIds = targets.Select(t => t.Id).ToList();

        double scaledCap = 0;

        if (isRateBased)
        {
            var rateLimits = await _rateLimitApi.GetByTargetTypeAsync(TargetType.Service);
            foreach (var target in targets)
            {
                var limit = rateLimits.FirstOrDefault(r => r.TargetId == target.Id);
                if (limit is not null)
                {
                    scaledCap += RateLimitCapScaler.ScaleRateLimitCap(
                        limit.MaxRequests,
                        limit.Window,
                        emptyAggregation.BucketDuration);
                }
            }
        }
        else
        {
            var pools = await _poolService.GetAllAsync();
            foreach (var target in targets)
            {
                scaledCap += pools.FirstOrDefault(p => p.Id == target.Id)?.MaxSlots ?? 0;
            }
        }

        var histories = await _statsService.GetHistoricalUsageAsync(
            context.SelectedFilterType, targetIds, null, from, now, context.TimeRange.Granularity);

        var aggregateLabel = isRateBased
            ? _localizer["Pages.Dashboard.Target.AllServices"]
            : _localizer["Pages.Dashboard.Target.AllResourcePools"];
        var targetPointLists = targets
            .Select(target => (IReadOnlyList<HistoricalUsagePoint>)(histories.FirstOrDefault(h => h.TargetId == target.Id)?.Points ?? []));
        var (clientAreas, referenceBuckets) = AggregateTargetChartSeriesBuilder.Build(
            targetPointLists,
            isRateBased,
            aggregateLabel,
            isRateBased ? DeniedViewMode.RateLimitDenied : DeniedViewMode.CapacityDenied,
            from,
            now,
            context.BucketCount,
            _localizer);

        var capPoints = referenceBuckets
            .Select(bucket => new ChartPoint(bucket.Label, scaledCap))
            .ToList();

        charts.Add(new TargetChartData(aggregateLabel, clientAreas, capPoints));
    }
}
