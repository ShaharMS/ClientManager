namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Precomputed global overview metrics updated on usage rollup.
/// </summary>
public record StatisticsOverviewSummary
{
    public const string DocumentId = "_meta:overview-summary";

    public required double RequestsPerMinute { get; init; }
    public required int TotalPoolSlots { get; init; }
    public required int AcquiredPoolSlots { get; init; }
    public required double AcquisitionPercentage { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}

/// <summary>
/// Latest granted/denied gauge per service and client for external metrics export.
/// </summary>
public record LatestUsageGaugeEntry(
    string ServiceId,
    string ClientId,
    long GrantedCount,
    long DeniedCount);

/// <summary>
/// Container for point-in-time usage gauges written on materialize.
/// </summary>
public record LatestUsageGaugesDocument
{
    public const string DocumentId = "_meta:latest-usage-gauges";

    public required IReadOnlyList<LatestUsageGaugeEntry> Entries { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}
