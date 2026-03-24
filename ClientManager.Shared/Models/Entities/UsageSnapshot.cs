using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// A time-series document recording usage for one client against one target at a given
/// <see cref="BucketGranularity"/>.
///
/// <para><strong>How snapshots are built</strong></para>
/// <para>
///     As <see cref="UsageEventType"/> events are emitted during access checks and resource
///     allocations, they accumulate in an in-memory buffer. A background
///     <c>UsagePersistenceService</c> periodically drains that buffer, groups the counts by
///     (client, target, granularity), and upserts them into the matching snapshot document.
///     Each flush appends or merges into the <see cref="Buckets"/> list.
/// </para>
///
/// <para><strong>Composite key</strong></para>
/// <para>
///     <see cref="Id"/> is deterministic: <c>"{ClientId}:{TargetType}:{TargetId}:{Granularity}"</c>.
///     This means there is exactly one document per (client, target, granularity) combination,
///     and every flush merges into the same document rather than creating a new one.
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
    public List<UsageBucket> Buckets { get; init; } = [];
}
