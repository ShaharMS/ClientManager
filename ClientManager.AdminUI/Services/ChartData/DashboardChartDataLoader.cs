using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;

namespace ClientManager.AdminUI.Services.ChartData;

public sealed class DashboardChartDataLoader
{
    private readonly DashboardDonutDataLoader _donutLoader;
    private readonly DashboardSingleTargetChartLoader _singleTargetLoader;
    private readonly DashboardAllTargetsChartLoader _allTargetsLoader;

    public DashboardChartDataLoader(
        StatisticsApiService statsService,
        ResourcePoolApiService poolService,
        GlobalRateLimitApiService rateLimitApi)
    {
        _donutLoader = new DashboardDonutDataLoader(statsService);
        _singleTargetLoader = new DashboardSingleTargetChartLoader(statsService, poolService, rateLimitApi);
        _allTargetsLoader = new DashboardAllTargetsChartLoader(statsService, poolService, rateLimitApi);
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

        return (newCharts, BuildDonutData(rawDonut));
    }

    public static List<ClientUsagePoint> ToDisplaySlices(IReadOnlyList<ClientUsagePoint> points)
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
            top.Add(new ClientUsagePoint(ChartAggregator.OthersId, ChartAggregator.OthersLabel, othersValue));
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

    private static DashboardDonutData BuildDonutData(List<ClientUsagePoint> points) =>
        new(ToDisplaySlices(points), GetOthersRestPool(points));
}
