using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Services;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class DashboardDonutDrillSelfCheck
{
    internal static void Run()
    {
        var othersLabel = "Others";
        var donut = new DashboardDonutData(
            DashboardChartDataLoader.ToDisplaySlices(Sample(12), othersLabel),
            DashboardChartDataLoader.GetOthersRestPool(Sample(12)));

        var depth = DashboardChartDataLoader.ReconcileDrillDepth(donut, 1, othersLabel);
        if (depth != 1)
        {
            throw new InvalidOperationException($"Expected drill depth 1, got {depth}.");
        }

        var shallow = new DashboardDonutData(
            DashboardChartDataLoader.ToDisplaySlices(Sample(3), othersLabel),
            DashboardChartDataLoader.GetOthersRestPool(Sample(3)));
        var reset = DashboardChartDataLoader.ReconcileDrillDepth(shallow, 2, othersLabel);
        if (reset != 0)
        {
            throw new InvalidOperationException($"Expected drill reset to 0, got {reset}.");
        }
    }

    private static List<ClientUsagePoint> Sample(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new ClientUsagePoint($"c{i}", $"Client {i}", i))
            .ToList();
}
