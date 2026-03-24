namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Time-series data for usage and capacity over time.
/// </summary>
/// <param name="UsagePoints">Data points representing actual usage over time.</param>
/// <param name="CapPoints">Data points representing the capacity limit over time.</param>
public record UsageTimeSeriesResponse(
    IReadOnlyList<TimeSeriesPoint> UsagePoints,
    IReadOnlyList<TimeSeriesPoint> CapPoints
);

/// <summary>
/// A single data point in a time series.
/// </summary>
/// <param name="Timestamp">The point in time this value represents.</param>
/// <param name="Value">The measured value at this timestamp.</param>
public record TimeSeriesPoint(
    DateTime Timestamp,
    double Value
);
