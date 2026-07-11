using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Utils;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Picks a single rollup tier for a statistics timeseries query.
/// </summary>
internal static class StatisticsGranularityPicker
{
    public static BucketGranularity PickForRange(DateTime fromUtc, DateTime toUtc, int bucketCount)
    {
        var range = toUtc - fromUtc;
        if (range <= TimeSpan.Zero || bucketCount <= 0)
        {
            return BucketGranularity.FiveMinute;
        }

        var inferred = InferFromRange(range);
        var minDuration = TimeSpan.FromTicks(range.Ticks / bucketCount);

        while (BucketGranularityHelper.GetRank(inferred) > BucketGranularityHelper.GetRank(BucketGranularity.Second)
               && BucketGranularityHelper.GetBucketDuration(inferred) > minDuration)
        {
            inferred = StepFiner(inferred);
        }

        return inferred;
    }

    private static BucketGranularity InferFromRange(TimeSpan range) => range switch
    {
        _ when range <= TimeSpan.FromMinutes(5) => BucketGranularity.Second,
        _ when range <= TimeSpan.FromHours(1) => BucketGranularity.OneMinute,
        _ when range <= TimeSpan.FromHours(6) => BucketGranularity.FiveMinute,
        _ when range <= TimeSpan.FromDays(7) => BucketGranularity.Hour,
        _ => BucketGranularity.Day
    };

    private static BucketGranularity StepFiner(BucketGranularity granularity) => granularity switch
    {
        BucketGranularity.Day => BucketGranularity.Hour,
        BucketGranularity.Hour => BucketGranularity.FiveMinute,
        BucketGranularity.FiveMinute => BucketGranularity.OneMinute,
        BucketGranularity.OneMinute => BucketGranularity.Second,
        _ => BucketGranularity.Second
    };
}
