using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Response containing historical usage time-series data for a target.
/// </summary>
public record HistoricalUsageResponse(
    string TargetId,
    TargetType TargetType,
    BucketGranularity Granularity,
    List<HistoricalUsagePoint> Points);

/// <summary>
/// A single data point in the historical usage time-series.
/// </summary>
public record HistoricalUsagePoint(
    DateTime Timestamp,
    long GrantedCount,
    long DeniedCount,
    long ReleasedCount,
    long ActiveCount);
