namespace ClientManager.AdminUI.Models;

public record TimeRangePreset(string Key, string Label, string Group, TimeSpan Duration, string Granularity)
{
    public DateTime GetFrom() => DateTime.UtcNow - Duration;
    public DateTime GetTo() => DateTime.UtcNow;

    public static readonly List<TimeRangePreset> All = new()
    {
        new("1m",  "1m",  "Minutes", TimeSpan.FromMinutes(1),  "FiveMinute"),
        new("5m",  "5m",  "Minutes", TimeSpan.FromMinutes(5),  "FiveMinute"),
        new("15m", "15m", "Minutes", TimeSpan.FromMinutes(15), "FiveMinute"),
        new("30m", "30m", "Minutes", TimeSpan.FromMinutes(30), "FiveMinute"),
        new("1h",  "1h",  "Hours",   TimeSpan.FromHours(1),    "FiveMinute"),
        new("3h",  "3h",  "Hours",   TimeSpan.FromHours(3),    "FiveMinute"),
        new("6h",  "6h",  "Hours",   TimeSpan.FromHours(6),    "FiveMinute"),
        new("12h", "12h", "Hours",   TimeSpan.FromHours(12),   "Hour"),
        new("1d",  "1d",  "Days",    TimeSpan.FromDays(1),     "Hour"),
        new("7d",  "7d",  "Days",    TimeSpan.FromDays(7),     "Hour"),
        new("30d", "30d", "Days",    TimeSpan.FromDays(30),    "Day"),
        new("90d", "90d", "Days",    TimeSpan.FromDays(90),    "Day"),
    };

    public static readonly TimeRangePreset Default = All.First(p => p.Key == "1h");

    public static TimeRangePreset? FindByKey(string? key) =>
        key is null ? null : All.FirstOrDefault(p => p.Key == key);
}
