namespace ClientManager.AdminUI.Models;

public record PollingIntervalPreset(string Key, string Label, TimeSpan Interval)
{
    public static readonly List<PollingIntervalPreset> All =
    [
        new("2s",  "2 seconds",  TimeSpan.FromSeconds(2)),
        new("5s",  "5 seconds",  TimeSpan.FromSeconds(5)),
        new("10s", "10 seconds", TimeSpan.FromSeconds(10)),
        new("30s", "30 seconds", TimeSpan.FromSeconds(30)),
        new("1m",  "1 minute",   TimeSpan.FromMinutes(1)),
        new("5m",  "5 minutes",  TimeSpan.FromMinutes(5)),
    ];

    public static readonly PollingIntervalPreset Default = All.First(p => p.Key == "10s");

    public static PollingIntervalPreset? FindByKey(string? key) =>
        key is null ? null : All.FirstOrDefault(p => p.Key == key);
}
