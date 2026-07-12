using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

/// <summary>ponytail: guards the all-target chart bucket aggregation.</summary>
internal static class TimeseriesChartAggregationSelfCheck
{
    internal static void Run()
    {
        var from = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var to = from.AddMinutes(5);
        var points = TimeseriesChartBuilder.AggregateDisplayBuckets(
            [
                new TimeseriesDisplayBucket("10:00", from, to, 2, 0, 0, 0, 0, 0, 0),
                new TimeseriesDisplayBucket("10:00", from, to, 3, 0, 0, 0, 0, 0, 0)
            ],
            bucket => bucket.GrantedCount);

        if (points.Count != 1 || points[0].Value != 5)
        {
            throw new InvalidOperationException("Timeseries target aggregation failed.");
        }
    }
}
