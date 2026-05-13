using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// Time-series data for usage and capacity over time.
/// </summary>
/// <param name="UsagePoints">Data points representing actual usage over time.</param>
/// <param name="CapPoints">Data points representing the capacity limit over time.</param>
public record UsageTimeSeriesResponse(
    IReadOnlyList<TimeSeriesPoint> UsagePoints,
    IReadOnlyList<TimeSeriesPoint> CapPoints);

/// <summary>
/// A single data point in a time series.
/// </summary>
/// <param name="Timestamp">The point in time this value represents.</param>
/// <param name="Value">The measured value at this timestamp.</param>
public record TimeSeriesPoint(
    DateTime Timestamp,
    double Value);

/// <summary>
/// Per-client usage breakdown for a specific service or resource pool.
/// </summary>
/// <param name="Entries">List of per-client usage entries.</param>
public record ClientUsageBreakdownResponse(
    IReadOnlyList<ClientUsageEntry> Entries);

/// <summary>
/// A single client's usage value within a breakdown.
/// </summary>
/// <param name="ClientId">The unique identifier of the client.</param>
/// <param name="ClientName">Human-readable display name of the client.</param>
/// <param name="GrantedCount">Total granted requests across the requested window.</param>
/// <param name="DeniedCount">Total denied requests across the requested window.</param>
/// <param name="ActiveCount">Latest active allocation count within the requested window.</param>
public record ClientUsageEntry(
    string ClientId,
    string ClientName,
    long GrantedCount,
    long DeniedCount,
    long ActiveCount);

/// <summary>
/// Response containing historical usage time-series data for a target.
/// </summary>
public record HistoricalUsageResponse(
    string TargetId,
    TargetType TargetType,
    BucketGranularity Granularity,
    IReadOnlyList<HistoricalUsagePoint> Points);

/// <summary>
/// Response containing historical usage time-series data for a target and client pair.
/// </summary>
public record ClientHistoricalUsageResponse(
    string TargetId,
    TargetType TargetType,
    string ClientId,
    BucketGranularity Granularity,
    IReadOnlyList<HistoricalUsagePoint> Points);

/// <summary>
/// A single data point in the historical usage time-series.
/// </summary>
public record HistoricalUsagePoint(
    DateTime Timestamp,
    long GrantedCount,
    long DeniedCount,
    long ReleasedCount,
    long ActiveCount);

/// <summary>
/// Per-target client usage breakdown within a batch response.
/// </summary>
/// <param name="TargetId">The service or resource pool ID.</param>
/// <param name="Entries">Per-client usage entries for this target.</param>
public record TargetClientUsageBreakdownResponse(
    string TargetId,
    IReadOnlyList<ClientUsageEntry> Entries);

/// <summary>
/// Per-target usage time-series within a batch response.
/// </summary>
/// <param name="TargetId">The service or resource pool ID.</param>
/// <param name="UsagePoints">Data points representing actual usage over time.</param>
/// <param name="CapPoints">Data points representing the capacity limit over time.</param>
public record TargetUsageTimeSeriesResponse(
    string TargetId,
    IReadOnlyList<TimeSeriesPoint> UsagePoints,
    IReadOnlyList<TimeSeriesPoint> CapPoints);
