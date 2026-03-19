using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Stores the time-series usage data for a single client-target combination at a specific granularity.
/// Each document contains an ordered list of time-bucketed counters.
/// </summary>
public record UsageSnapshot
{
    /// <summary>
    /// Compound key: "{ClientId}:{TargetType}:{TargetId}:{Granularity}"
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// ID of the client this usage data belongs to.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// ID of the service or resource pool being tracked.
    /// </summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Whether the target is a Service or ResourcePool.
    /// </summary>
    public GlobalRateLimitTarget TargetType { get; init; }

    /// <summary>
    /// The time granularity of the buckets in this snapshot.
    /// </summary>
    public BucketGranularity Granularity { get; init; }

    /// <summary>
    /// Ordered list of usage buckets, oldest first.
    /// </summary>
    public List<UsageBucket> Buckets { get; init; } = new();
}
