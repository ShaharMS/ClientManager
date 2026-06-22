using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Services;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Utils;

public static class DeniedBreakdownHelper
{
    public static bool ShowBreakdown(IEnumerable<string>? selectedClientIds) =>
        (selectedClientIds?.Count() ?? 0) <= 1;

    public static (long Unauth, long Blocked, long Third) GetTriplet(
        long unauth, long blocked, long rateLimited, long capacity, DeniedViewMode mode) =>
        mode == DeniedViewMode.CapacityDenied
            ? (unauth, blocked, capacity)
            : (unauth, blocked, rateLimited);

    public static (long Unauth, long Blocked, long Third) GetTriplet(ClientUsageEntry entry, DeniedViewMode mode) =>
        GetTriplet(
            entry.DeniedUnauthenticatedCount,
            entry.DeniedBlockedCount,
            entry.DeniedRateLimitedCount,
            entry.DeniedCapacityLimitedCount,
            mode);

    public static string ThirdLabel(DeniedViewMode mode) =>
        mode == DeniedViewMode.CapacityDenied ? "Capacity limited" : "Rate limited";

    public static string BuildTooltip(long unauth, long blocked, long third, DeniedViewMode mode) =>
        $"Unauthenticated: {unauth:N0}\nBlocked: {blocked:N0}\n{ThirdLabel(mode)}: {third:N0}";

    public static double GetDeniedCategoryValue(HistoricalUsagePoint point, string seriesSuffix) =>
        seriesSuffix switch
        {
            ChartAggregator.DeniedUnauthSuffix => point.DeniedUnauthenticatedCount,
            ChartAggregator.DeniedBlockedSuffix => point.DeniedBlockedCount,
            ChartAggregator.DeniedRateLimitedSuffix => point.DeniedRateLimitedCount,
            ChartAggregator.DeniedCapacitySuffix => point.DeniedCapacityLimitedCount,
            _ => point.DeniedCount
        };
}
