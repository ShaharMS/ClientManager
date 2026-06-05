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

    public async Task<(List<TargetChartData> Charts, List<ClientUsagePoint> Donut)> LoadAsync(
        DashboardChartLoadContext context)
    {
        var newCharts = new List<TargetChartData>();
        List<ClientUsagePoint> newDonut;

        if (context.SelectedTargetId == DashboardChartLoadContext.AllTargetsId)
        {
            await _allTargetsLoader.LoadAsync(context, newCharts);
            newDonut = await _donutLoader.LoadAllTargetsAsync(context);
        }
        else
        {
            newDonut = await _singleTargetLoader.LoadAsync(context, newCharts);
        }

        if (newDonut.Count > ChartAggregator.DefaultTopN)
        {
            var ranked = newDonut.OrderByDescending(p => p.Value).ToList();
            var top = ranked.Take(ChartAggregator.DefaultTopN).ToList();
            var othersValue = ranked.Skip(ChartAggregator.DefaultTopN).Sum(p => p.Value);
            top.Add(new ClientUsagePoint(ChartAggregator.OthersId, ChartAggregator.OthersLabel, othersValue));
            newDonut = top;
        }

        return (newCharts, newDonut);
    }
}
