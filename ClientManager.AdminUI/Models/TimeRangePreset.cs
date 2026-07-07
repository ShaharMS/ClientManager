using System.Globalization;
using ClientManager.AdminUI.Resources;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Models;

public record TimeRangePreset(string Key, string Label, string Group, TimeSpan Duration, string Granularity)
{
    public string GetLocalizedLabel(IStringLocalizer<SharedResources> localizer) =>
        localizer[$"Presets.TimeRange.{Key}.Label"];

    public string GetLocalizedGroup(IStringLocalizer<SharedResources> localizer) =>
        localizer[$"Presets.TimeRange.Group.{Group}"];

    public DateTime GetFrom(DateTime to) => to - Duration;
    public DateTime GetFrom() => DateTime.UtcNow - Duration;
    public DateTime GetTo() => DateTime.UtcNow;

    public string FormatTimestamp(DateTime timestamp, bool invariant = false)
    {
        var culture = invariant ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
        var local = timestamp.ToLocalTime();
        return Granularity switch
        {
            "Second" => local.ToString("T", culture),
            "OneMinute" => local.ToString("HH:mm", culture),
            "Day" => local.ToString("MMM dd", culture),
            "Hour" => local.ToString("MMM dd HH:mm", culture),
            _ => local.ToString("HH:mm", culture)
        };
    }

    public static readonly List<TimeRangePreset> All =
    [
        new("1m",  "Last minute",     "Minutes", TimeSpan.FromMinutes(1),  "Second"),
        new("5m",  "Last 5 minutes",  "Minutes", TimeSpan.FromMinutes(5),  "Second"),
        new("15m", "Last 15 minutes", "Minutes", TimeSpan.FromMinutes(15), "OneMinute"),
        new("30m", "Last 30 minutes", "Minutes", TimeSpan.FromMinutes(30), "OneMinute"),
        new("1h",  "Last hour",       "Hours",   TimeSpan.FromHours(1),    "FiveMinute"),
        new("3h",  "Last 3 hours",    "Hours",   TimeSpan.FromHours(3),    "FiveMinute"),
        new("6h",  "Last 6 hours",    "Hours",   TimeSpan.FromHours(6),    "FiveMinute"),
        new("12h", "Last 12 hours",   "Hours",   TimeSpan.FromHours(12),   "Hour"),
        new("1d",  "Last 24 hours",   "Days",    TimeSpan.FromDays(1),     "Hour"),
        new("7d",  "Last 7 days",     "Days",    TimeSpan.FromDays(7),     "Hour"),
        new("30d", "Last 30 days",    "Days",    TimeSpan.FromDays(30),    "Day"),
        new("90d", "Last 90 days",    "Days",    TimeSpan.FromDays(90),    "Day"),
    ];

    public static readonly TimeRangePreset Default = All.First(p => p.Key == "1h");

    public static TimeRangePreset? FindByKey(string? key) =>
        key is null ? null : All.FirstOrDefault(p => p.Key == key);
}
