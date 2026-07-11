using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// A single display bucket in a statistics timeseries search response.
/// </summary>
public record TimeseriesDisplayBucket(
    string Label,
    DateTime BucketStartUtc,
    DateTime BucketEndUtc,
    long GrantedCount,
    long DeniedUnauthenticatedCount,
    long DeniedBlockedCount,
    long DeniedRateLimitedCount,
    long DeniedCapacityLimitedCount,
    long ReleasedCount,
    long ActiveCount);

/// <summary>
/// Per-client bucketed series within a target chart.
/// </summary>
public record TimeseriesClientSeries(
    string ClientId,
    string ClientName,
    IReadOnlyList<TimeseriesDisplayBucket> Buckets);

/// <summary>
/// Target-level aggregate buckets (denied overlay) plus per-client series.
/// </summary>
public record TimeseriesTargetSeries(
    string TargetId,
    string TargetName,
    double CapValue,
    IReadOnlyList<TimeseriesDisplayBucket> AggregateBuckets,
    IReadOnlyList<TimeseriesClientSeries> ClientSeries);

/// <summary>
/// Chart-ready statistics timeseries search result.
/// </summary>
public record TimeseriesSearchResponse(
    StatisticsSearchCategory SearchCategory,
    TargetType TargetType,
    BucketGranularity SourceGranularity,
    IReadOnlyList<TimeseriesTargetSeries> Targets);
