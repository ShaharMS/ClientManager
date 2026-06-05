using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Services;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Utils;

public static class ChartValueHelper
{
    public static ChartBucketAggregator.AggregationMode GetAggregationMode(bool isRateBased) =>
        isRateBased
            ? ChartBucketAggregator.AggregationMode.Sum
            : ChartBucketAggregator.AggregationMode.Latest;

    public static double GetHistoricalPointValue(HistoricalUsagePoint point, bool isRateBased) =>
        isRateBased ? point.GrantedCount : point.ActiveCount;

    public static double GetClientUsageValue(IEnumerable<HistoricalUsagePoint> points, bool isRateBased)
    {
        if (isRateBased)
        {
            return points.Sum(point => (double)point.GrantedCount);
        }

        return points
            .OrderByDescending(point => point.Timestamp)
            .Select(point => (double)point.ActiveCount)
            .FirstOrDefault();
    }

    public static List<ChartPoint> SumBuckets(
        IEnumerable<ChartBucketAggregator.AggregationResult> aggregations,
        IReadOnlyList<ChartBucketAggregator.AggregatedBucket> referenceBuckets)
    {
        var aggregationList = aggregations.ToList();
        return referenceBuckets
            .Select((bucket, index) => new ChartPoint(
                bucket.Label,
                aggregationList.Sum(aggregation => aggregation.Buckets[index].Value)))
            .ToList();
    }
}
