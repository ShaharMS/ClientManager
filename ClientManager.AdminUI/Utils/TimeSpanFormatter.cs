using System.Globalization;
using ClientManager.AdminUI.Resources;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Utils;

/// <summary>
/// Compact duration labels for rate-limit windows and pool TTLs.
/// </summary>
public static class TimeSpanFormatter
{
    public static string FormatCompact(
        TimeSpan value,
        IStringLocalizer<SharedResources> localizer)
    {
        var culture = CultureInfo.CurrentCulture;
        if (value.TotalHours >= 1)
        {
            return string.Format(
                culture,
                localizer["Units.Duration.HoursCompact"],
                value.TotalHours.ToString("0.#", culture));
        }

        if (value.TotalMinutes >= 1)
        {
            return string.Format(
                culture,
                localizer["Units.Duration.MinutesCompact"],
                value.TotalMinutes.ToString("0.#", culture));
        }

        return string.Format(
            culture,
            localizer["Units.Duration.SecondsCompact"],
            value.TotalSeconds.ToString("0.#", culture));
    }

    public static string FormatRequestsWindow(
        int maxRequests,
        TimeSpan window,
        IStringLocalizer<SharedResources> localizer) =>
        $"{maxRequests.ToString(CultureInfo.CurrentCulture)} / {FormatCompact(window, localizer)}";
}
