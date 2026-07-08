using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

public sealed class DashboardChartDataLoader
{
    private readonly DashboardDonutDataLoader _donutLoader;
    private readonly DashboardSingleTargetChartLoader _singleTargetLoader;
    private readonly DashboardAllTargetsChartLoader _allTargetsLoader;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private List<ClientUsagePoint>? _cachedRawDonut;

    public DashboardChartDataLoader(
        StatisticsApiService statsService,
        ResourcePoolApiService poolService,
        GlobalRateLimitApiService rateLimitApi,
        IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
        _donutLoader = new DashboardDonutDataLoader(statsService);
        _singleTargetLoader = new DashboardSingleTargetChartLoader(statsService, poolService, rateLimitApi, localizer);
        _allTargetsLoader = new DashboardAllTargetsChartLoader(statsService, poolService, rateLimitApi, localizer);
    }

    public async Task<(List<TargetChartData> Charts, DashboardDonutData Donut)> LoadAsync(
        DashboardChartLoadContext context)
    {
        var newCharts = new List<TargetChartData>();
        List<ClientUsagePoint> rawDonut;

        if (context.SelectedTargetId == DashboardChartLoadContext.AllTargetsId)
        {
            await _allTargetsLoader.LoadAsync(context, newCharts);
            rawDonut = await _donutLoader.LoadAllTargetsAsync(context);
        }
        else
        {
            rawDonut = await _singleTargetLoader.LoadAsync(context, newCharts);
        }

        _cachedRawDonut = rawDonut;
        return (newCharts, BuildDonutData(rawDonut, _localizer["Common.Others"]));
    }

    public bool TryRebuildFromCache(
        DashboardChartLoadContext context,
        out List<TargetChartData> charts,
        out DashboardDonutData donut)
    {
        charts = new List<TargetChartData>();
        if (context.SelectedTargetId == DashboardChartLoadContext.AllTargetsId)
        {
            if (!_allTargetsLoader.TryRebuildFromCache(context, charts) || _cachedRawDonut is null)
            {
                donut = new DashboardDonutData([], []);
                return false;
            }

            donut = BuildDonutData(_cachedRawDonut, _localizer["Common.Others"]);
            return true;
        }

        if (!_singleTargetLoader.TryRebuildFromCache(context, charts, out var singleDonut))
        {
            donut = new DashboardDonutData([], []);
            return false;
        }

        donut = BuildDonutData(singleDonut, _localizer["Common.Others"]);
        return true;
    }

    public static List<ClientUsagePoint> ToDisplaySlices(
        IReadOnlyList<ClientUsagePoint> points,
        string othersLabel)
    {
        if (points.Count <= ChartAggregator.DefaultTopN)
        {
            return points.OrderByDescending(p => p.Value).ToList();
        }

        var ranked = points.OrderByDescending(p => p.Value).ToList();
        var top = ranked.Take(ChartAggregator.DefaultTopN).ToList();
        var othersValue = ranked.Skip(ChartAggregator.DefaultTopN).Where(p => p.Value > 0).Sum(p => p.Value);
        if (othersValue > 0)
        {
            top.Add(new ClientUsagePoint(ChartAggregator.OthersId, othersLabel, othersValue));
        }

        return top;
    }

    public static List<ClientUsagePoint> GetOthersRestPool(IReadOnlyList<ClientUsagePoint> points) =>
        points.Count <= ChartAggregator.DefaultTopN
            ? []
            : points.OrderByDescending(p => p.Value)
                .Skip(ChartAggregator.DefaultTopN)
                .Where(p => p.Value > 0)
                .ToList();

    public static List<ClientUsagePoint> GetPoolAtDrillDepth(DashboardDonutData donut, int depth)
    {
        if (depth < 1)
        {
            return [];
        }

        var pool = donut.OthersBreakdown;
        for (var i = 1; i < depth; i++)
        {
            pool = GetOthersRestPool(pool);
            if (pool.Count == 0)
            {
                return [];
            }
        }

        return pool;
    }

    // ponytail: O(depth * n); depth is tiny (≤4), n is client count
    public static int ReconcileDrillDepth(DashboardDonutData donut, int depth, string othersLabel)
    {
        while (depth > 0)
        {
            var pool = GetPoolAtDrillDepth(donut, depth);
            if (pool.Count == 0)
            {
                depth--;
                continue;
            }

            if (depth == 1)
            {
                if (!donut.Slices.Any(p => p.ClientId == ChartAggregator.OthersId)
                    || donut.OthersBreakdown.Count == 0)
                {
                    return 0;
                }
            }
            else
            {
                var parentDisplay = ToDisplaySlices(GetPoolAtDrillDepth(donut, depth - 1), othersLabel);
                if (!parentDisplay.Any(p => p.ClientId == ChartAggregator.OthersId))
                {
                    depth--;
                    continue;
                }
            }

            return depth;
        }

        return 0;
    }

    public static bool CanDrillIntoOthers(DashboardDonutData donut, int depth) =>
        depth == 0
            ? donut.OthersBreakdown.Count > 0
            : GetPoolAtDrillDepth(donut, depth).Count > ChartAggregator.DefaultTopN;

    private static DashboardDonutData BuildDonutData(List<ClientUsagePoint> points, string othersLabel) =>
        new(ToDisplaySlices(points, othersLabel), GetOthersRestPool(points));
}
