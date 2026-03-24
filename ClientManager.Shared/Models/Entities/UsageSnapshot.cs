using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Represents usage data for a given client &amp; target combination over a continuous time range, aggregated into buckets of a specific <see cref="BucketGranularity"/>.
/// <para>
///     This is used to generate usage reports for clients over a period of time 
///     (for example, give me the last 30 days of accesses client <c>X</c> made for service <c>Y</c>)
/// </para>
/// </summary>
public record UsageSnapshot
{
    /// <summary>
    /// A unique identifier for this usage snapshot document.
    /// <br></br>
    /// Composed using <c>"{ClientId}:{TargetType}:{TargetId}:{Granularity}"</c>
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
    /// The type of target this usage data is tracking.
    /// </summary>
    public TargetType TargetType { get; init; }

    /// <summary>
    /// The "exactness" of data in this snapshot - how aggregated are the time buckets in the snapshot.
    /// </summary>
    public BucketGranularity Granularity { get; init; }

    /// <summary>
    /// Ordered list of usage aggregations over time, oldest first.
    /// </summary>
    public List<UsageBucket> Buckets { get; init; } = new();
}
