using System.Globalization;
using ClientManager.AdminUI.Localization;
using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Utils;

public class DeniedBreakdownFormatter
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeniedBreakdownFormatter(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
    }

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

    public string ThirdLabel(DeniedViewMode mode) =>
        mode == DeniedViewMode.CapacityDenied
            ? _localizer[TermKeys.DeniedOutOfSlots]
            : _localizer[TermKeys.DeniedThrottled];

    public string BuildTooltip(long unauth, long blocked, long third, DeniedViewMode mode) =>
        string.Format(
            CultureInfo.CurrentCulture,
            _localizer["Terms.Denied.Tooltip"],
            unauth.ToString("N0", CultureInfo.CurrentCulture),
            blocked.ToString("N0", CultureInfo.CurrentCulture),
            ThirdLabel(mode),
            third.ToString("N0", CultureInfo.CurrentCulture));

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
