using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class AllocationsChartPointHelper
{
    internal static double GetHistoricalPointValue(HistoricalUsagePoint point, bool isAccessMetric)
        => isAccessMetric ? point.GrantedCount : point.ActiveCount;
}
