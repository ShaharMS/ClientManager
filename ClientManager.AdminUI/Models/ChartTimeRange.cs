namespace ClientManager.AdminUI.Models;

using System.Globalization;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Utils;
using Microsoft.Extensions.Localization;

public enum ChartTimeRangeMode
{
    Relative,
    Custom
}

public sealed class ChartTimeRange
{
    public ChartTimeRangeMode Mode { get; private init; }
    public TimeSpan RelativeDuration { get; private init; }
    public DateTime CustomFromUtc { get; private init; }
    public DateTime CustomToUtc { get; private init; }

    public static ChartTimeRange FromRelative(TimeSpan duration) => new()
    {
        Mode = ChartTimeRangeMode.Relative,
        RelativeDuration = duration > TimeSpan.Zero ? duration : TimeSpan.FromHours(1)
    };

    public static ChartTimeRange FromPreset(TimeRangePreset preset) =>
        FromRelative(preset.Duration);

    public static ChartTimeRange FromCustom(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc < fromUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return new ChartTimeRange
        {
            Mode = ChartTimeRangeMode.Custom,
            CustomFromUtc = fromUtc,
            CustomToUtc = toUtc
        };
    }

    public DateTime GetTo() => Mode == ChartTimeRangeMode.Custom
        ? CustomToUtc
        : DateTime.UtcNow;

    public DateTime GetFrom(DateTime to) => Mode switch
    {
        ChartTimeRangeMode.Relative => to - RelativeDuration,
        ChartTimeRangeMode.Custom => CustomFromUtc,
        _ => to - TimeSpan.FromHours(1)
    };

    public DateTime GetFrom() => GetFrom(GetTo());

    public string Granularity => Mode == ChartTimeRangeMode.Relative
        ? InferGranularity(RelativeDuration)
        : InferGranularity(CustomToUtc - CustomFromUtc);

    public string GetDisplayLabel()
    {
        if (Mode == ChartTimeRangeMode.Relative)
        {
            return FormatRelativeLabel(RelativeDuration);
        }

        return FormatCustomLabel();
    }

    public string GetLocalizedDisplayLabel(IStringLocalizer<SharedResources> localizer)
    {
        if (Mode == ChartTimeRangeMode.Relative)
        {
            return FormatRelativeLabel(RelativeDuration, localizer);
        }

        return FormatCustomLabel();
    }

    private string FormatCustomLabel()
    {
        var culture = CultureInfo.CurrentCulture;
        var fromLocal = CustomFromUtc.ToLocalTime();
        var toLocal = CustomToUtc.ToLocalTime();
        if (fromLocal.Date == toLocal.Date)
        {
            return $"{CultureDateFormats.ChartCustomRange(fromLocal, culture)} – {toLocal.ToString("HH:mm", culture)}";
        }

        return $"{CultureDateFormats.ChartCustomRange(fromLocal, culture)} – {CultureDateFormats.ChartCustomRange(toLocal, culture)}";
    }

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

    private static string InferGranularity(TimeSpan duration)
    {
        if (duration.TotalMinutes <= 5)
        {
            return "Second";
        }

        if (duration.TotalMinutes <= 60)
        {
            return "OneMinute";
        }

        if (duration.TotalHours <= 6)
        {
            return "FiveMinute";
        }

        if (duration.TotalDays <= 7)
        {
            return "Hour";
        }

        return "Day";
    }

    private static string FormatRelativeLabel(TimeSpan duration) =>
        FormatRelativeLabel(duration, null);

    private static string FormatRelativeLabel(TimeSpan duration, IStringLocalizer<SharedResources>? localizer)
    {
        var culture = CultureInfo.CurrentCulture;

        if (duration.TotalDays >= 1 && duration.TotalDays % 1 == 0)
        {
            var days = (int)duration.TotalDays;
            if (localizer is null)
            {
                return $"Last {days} day{(days == 1 ? "" : "s")}";
            }

            return days == 1
                ? localizer["Charts.TimeRange.LastDay"]
                : string.Format(culture, localizer["Charts.TimeRange.LastDays"], days);
        }

        if (duration.TotalHours >= 1 && duration.TotalHours % 1 == 0)
        {
            var hours = (int)duration.TotalHours;
            if (localizer is null)
            {
                return $"Last {hours} hour{(hours == 1 ? "" : "s")}";
            }

            return hours == 1
                ? localizer["Charts.TimeRange.LastHour"]
                : string.Format(culture, localizer["Charts.TimeRange.LastHours"], hours);
        }

        if (duration.TotalMinutes >= 1 && duration.TotalMinutes % 1 == 0)
        {
            var minutes = (int)duration.TotalMinutes;
            if (localizer is null)
            {
                return $"Last {minutes} minute{(minutes == 1 ? "" : "s")}";
            }

            return minutes == 1
                ? localizer["Charts.TimeRange.LastMinute"]
                : string.Format(culture, localizer["Charts.TimeRange.LastMinutes"], minutes);
        }

        var seconds = (int)duration.TotalSeconds;
        if (localizer is null)
        {
            return $"Last {seconds} second{(seconds == 1 ? "" : "s")}";
        }

        return seconds == 1
            ? localizer["Charts.TimeRange.LastSecond"]
            : string.Format(culture, localizer["Charts.TimeRange.LastSeconds"], seconds);
    }
}
