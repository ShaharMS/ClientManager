namespace ClientManager.AdminUI.Models;

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

        var fromLocal = CustomFromUtc.ToLocalTime();
        var toLocal = CustomToUtc.ToLocalTime();
        if (fromLocal.Date == toLocal.Date)
        {
            return $"{fromLocal:MMM d, HH:mm} – {toLocal:HH:mm}";
        }

        return $"{fromLocal:MMM d, HH:mm} – {toLocal:MMM d, HH:mm}";
    }

    public string FormatTimestamp(DateTime timestamp)
    {
        var local = timestamp.ToLocalTime();
        return Granularity switch
        {
            "Second" => local.ToString("HH:mm:ss"),
            "Day" => local.ToString("MMM dd"),
            "Hour" => local.ToString("MMM dd HH:mm"),
            _ => local.ToString("HH:mm")
        };
    }

    private static string InferGranularity(TimeSpan duration)
    {
        if (duration.TotalMinutes <= 5)
        {
            return "Second";
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

    private static string FormatRelativeLabel(TimeSpan duration)
    {
        if (duration.TotalDays >= 1 && duration.TotalDays % 1 == 0)
        {
            var days = (int)duration.TotalDays;
            return $"Last {days} day{(days == 1 ? "" : "s")}";
        }

        if (duration.TotalHours >= 1 && duration.TotalHours % 1 == 0)
        {
            var hours = (int)duration.TotalHours;
            return $"Last {hours} hour{(hours == 1 ? "" : "s")}";
        }

        if (duration.TotalMinutes >= 1 && duration.TotalMinutes % 1 == 0)
        {
            var minutes = (int)duration.TotalMinutes;
            return $"Last {minutes} minute{(minutes == 1 ? "" : "s")}";
        }

        var seconds = (int)duration.TotalSeconds;
        return $"Last {seconds} second{(seconds == 1 ? "" : "s")}";
    }
}
