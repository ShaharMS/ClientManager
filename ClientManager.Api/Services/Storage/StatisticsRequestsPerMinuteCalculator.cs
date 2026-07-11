using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Utils;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Sums granted service requests over a rolling window and normalizes to requests per minute.
/// </summary>
internal static class StatisticsRequestsPerMinuteCalculator
{
    internal static double Compute(
        Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, BucketTotals>> totalsByTargetClient,
        DateTime fromUtc,
        DateTime toUtc,
        BucketGranularity granularity,
        int windowMinutes)
    {
        var bucketDuration = BucketGranularityHelper.GetBucketDuration(granularity);
        long granted = 0;

        foreach (var (_, buckets) in totalsByTargetClient)
        {
            foreach (var (timestamp, bucketTotals) in buckets)
            {
                if (BucketGranularityHelper.OverlapsRange(timestamp, bucketDuration, fromUtc, toUtc))
                {
                    granted += bucketTotals.Granted;
                }
            }
        }

        return Math.Round(granted / (double)windowMinutes, 1);
    }
}
