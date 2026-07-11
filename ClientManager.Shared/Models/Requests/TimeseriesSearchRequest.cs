using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Requests;

/// <summary>
/// Filtered timeseries search for dashboard and monitor charts.
/// </summary>
public record TimeseriesSearchRequest
{
    public required StatisticsSearchCategory SearchCategory { get; init; }
    public IReadOnlyList<string>? TargetIds { get; init; }
    public IReadOnlyList<string>? ClientIds { get; init; }
    public required DateTime FromUtc { get; init; }
    public required DateTime ToUtc { get; init; }
    public int BucketCount { get; init; } = 20;
}
