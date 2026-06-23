using System.Globalization;
using ClientManager.AdminUI.Resources;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

namespace ClientManager.AdminUI.Utils;

public static class UtilizationHelper
{
    public static ProgressBarStyle GetProgressBarStyle(int percent) =>
        percent >= 100 ? ProgressBarStyle.Danger
            : percent >= 75 ? ProgressBarStyle.Warning
            : ProgressBarStyle.Success;

    public static string FormatCap(int capValue) => capValue > 0 ? capValue.ToString("N0") : "-";

    public static string FormatCapDisplay(int capValue) => capValue > 0 ? capValue.ToString() : "-";

    public static string FormatRemaining(long? remainingValue) =>
        remainingValue.HasValue ? remainingValue.Value.ToString("N0") : "-";

    public static string FormatRequestsPerMinute(
        IStringLocalizer<SharedResources> localizer,
        int requestsPerMinute) =>
        requestsPerMinute > 0
            ? string.Format(
                CultureInfo.CurrentCulture,
                localizer["Units.RequestsPerMinute"],
                requestsPerMinute.ToString("N0", CultureInfo.CurrentCulture))
            : "-";
}
