using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

public sealed class DashboardChartDataLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private TimeseriesSearchResponse? _cachedResponse;
    private string? _cachedDataKey;

    public DashboardChartDataLoader(
        StatisticsApiService statsService,
        IStringLocalizer<SharedResources> localizer)
    {
        _statsService = statsService;
        _localizer = localizer;
    }

    public async Task<(List<TargetChartData> Charts, DashboardDonutData Donut)> LoadAsync(
        DashboardChartLoadContext context)
    {
        var dataKey = BuildDataCacheKey(context);
        if (_cachedResponse is null || _cachedDataKey != dataKey)
        {
            _cachedResponse = await TimeseriesChartBuilder.FetchDashboardAsync(_statsService, context);
            _cachedDataKey = dataKey;
        }

        var (charts, rawDonut) = TimeseriesChartBuilder.BuildDashboardFromResponse(
            _cachedResponse,
            context,
            _localizer);

        return (charts, BuildDonutData(rawDonut, _localizer["Common.Others"]));
    }

    public bool TryRebuildFromCache(
        DashboardChartLoadContext context,
        out List<TargetChartData> charts,
        out DashboardDonutData donut)
    {
        if (_cachedResponse is null || _cachedDataKey != BuildDataCacheKey(context))
        {
            charts = [];
            donut = new DashboardDonutData([], []);
            return false;
        }

        (charts, var rawDonut) = TimeseriesChartBuilder.BuildDashboardFromResponse(
            _cachedResponse,
            context,
            _localizer);
        donut = BuildDonutData(rawDonut, _localizer["Common.Others"]);
        return true;
    }

    private static string BuildDataCacheKey(DashboardChartLoadContext context) =>
        $"{context.SelectedFilterType}|{context.SelectedTargetId}|{string.Join(',', context.SelectedClientIds ?? [])}|{context.TimeRange.GetFrom():O}|{context.TimeRange.GetTo():O}|{context.TimeRange.Granularity}";

    public static List<ClientUsagePoint> ToDisplaySlices(
        IReadOnlyList<ClientUsagePoint> points,
        string othersLabel)
    {
        var aggregated = ChartAggregator.Aggregate(
            points.Select(point => new ChartAggregator.AggregatedSeries(
                point.ClientId,
                point.ClientName,
                [new ChartAggregator.AggregatedPoint(point.ClientName, point.Value)]))
            .ToList());

        return aggregated
            .Select(point => new ClientUsagePoint(point.Id, point.Name, point.Points[0].Value))
            .ToList();
    }

    public static bool CanDrillIntoOthers(DashboardDonutData donut, int drillDepth) =>
        drillDepth == 0 && donut.OthersBreakdown.Count > 0;

    public static IReadOnlyList<ClientUsagePoint> GetPoolAtDrillDepth(DashboardDonutData donut, int drillDepth) =>
        drillDepth > 0 ? donut.OthersBreakdown : donut.Slices;

    public static List<ClientUsagePoint> GetOthersRestPool(IReadOnlyList<ClientUsagePoint> rawSlices) =>
        ChartAggregator.Aggregate(
            rawSlices.Select(point => new ChartAggregator.AggregatedSeries(
                point.ClientId,
                point.ClientName,
                [new ChartAggregator.AggregatedPoint(point.ClientName, point.Value)]))
            .ToList())
            .Where(point => point.Id == ChartAggregator.OthersId)
            .SelectMany(point => point.Points.Select(p => new ClientUsagePoint(ChartAggregator.OthersId, p.Label, p.Value)))
            .ToList();

    public static int ReconcileDrillDepth(DashboardDonutData donut, int drillDepth, string othersLabel) =>
        drillDepth > 0 && donut.OthersBreakdown.Count == 0 ? 0 : drillDepth;

    private static DashboardDonutData BuildDonutData(List<ClientUsagePoint> rawDonut, string othersLabel)
    {
        var slices = ToDisplaySlices(rawDonut, othersLabel);
        var othersBreakdown = ChartAggregator.Aggregate(
            rawDonut.Select(point => new ChartAggregator.AggregatedSeries(
                point.ClientId,
                point.ClientName,
                [new ChartAggregator.AggregatedPoint(point.ClientName, point.Value)]))
            .ToList())
            .Where(point => point.Id == ChartAggregator.OthersId)
            .SelectMany(point => point.Points.Select(p => new ClientUsagePoint(point.Id, p.Label, p.Value)))
            .ToList();

        return new DashboardDonutData(slices, othersBreakdown);
    }
}
