using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Utils;

namespace ClientManager.AdminUI.Utils;

public static class ChartGranularityHelper
{
    public static TimeSpan GetStorageBucketDuration(string? granularity)
    {
        var parsed = BucketGranularityHelper.TryParse(granularity);
        return parsed is null ? TimeSpan.Zero : BucketGranularityHelper.GetBucketDuration(parsed.Value);
    }

    public static TimeSpan GetStorageBucketDuration(BucketGranularity granularity) =>
        BucketGranularityHelper.GetBucketDuration(granularity);
}
