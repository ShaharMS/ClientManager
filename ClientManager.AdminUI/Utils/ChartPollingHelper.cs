namespace ClientManager.AdminUI.Utils;

using ClientManager.AdminUI.Models;

public static class ChartPollingHelper
{
    public static PollingIntervalPreset SuggestForRange(ChartTimeRange range)
    {
        var duration = range.Mode == ChartTimeRangeMode.Relative
            ? range.RelativeDuration
            : range.CustomToUtc - range.CustomFromUtc;

        if (duration < TimeSpan.FromHours(1))
        {
            return PollingIntervalPreset.FindByKey("5s") ?? PollingIntervalPreset.Default;
        }

        if (duration < TimeSpan.FromDays(7))
        {
            return PollingIntervalPreset.FindByKey("30s") ?? PollingIntervalPreset.Default;
        }

        return PollingIntervalPreset.FindByKey("2m") ?? PollingIntervalPreset.FindByKey("5m") ?? PollingIntervalPreset.Default;
    }
}
