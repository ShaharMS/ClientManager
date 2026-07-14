namespace ClientManager.Api.Utils.Instrumentation;

/// <summary>
/// Explicit histogram upper bounds (milliseconds) for ClientManager duration instruments.
/// </summary>
/// <remarks>
/// Default OTel buckets start at 5ms+, which makes sub-millisecond cache hits look like a flat 2.5ms median.
/// </remarks>
internal static class DurationHistogramBuckets
{
    public static readonly double[] Milliseconds =
    [
        0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10,
        25, 50, 100, 250, 500, 1000, 2500, 5000,
    ];
}
