using System.Globalization;
using ClientManager.AdminUI.Localization;

namespace ClientManager.AdminUI.Utils;

public static class CultureDateFormats
{
    public static bool IsHebrew(CultureInfo? culture = null) =>
        SupportedCultures.IsRtl((culture ?? CultureInfo.CurrentCulture).Name);

    public static string ChartPicker(CultureInfo? culture = null) =>
        IsHebrew(culture)
            ? "HH:mm dd 'ב'MMMM yyyy"
            : "MMM dd yyyy HH:mm";

    public static string ChartCustomRange(DateTime local, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return local.ToString(IsHebrew(culture) ? "HH:mm dd 'ב'MMMM yyyy" : "MMM d, HH:mm", culture);
    }
}
