namespace ClientManager.Api.Models.Responses;

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
