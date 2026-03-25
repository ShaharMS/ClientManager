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
///     service periodically drains that buffer, groups the counts by
///     (client, target, granularity), and upserts them into the matching snapshot document.
///     Each flush appends or merges into the <see cref="Buckets"/> list.
/// </para>
///
/// <para><strong>Time segments</strong></para>
/// <para>
///     Snapshots are split into bounded <em>time segments</em> so that no single document
///     grows indefinitely. Each segment covers a fixed time window whose size depends on
///     the granularity (e.g. 1 hour for second-granularity, 1 day for five-minute, 1 week
///     for hour, 1 month for day). The <see cref="SegmentStart"/> property records which
///     window this document belongs to, and the <see cref="Id"/> includes a
///     <c>yyyyMMddHH</c> suffix that makes each segment a separate document. This design
///     means flush only reads and writes the current small segment, prune can drop whole
///     segment documents instead of filtering individual buckets, and retention can grow
///     without any single document becoming too large to handle efficiently.
/// </para>
///
/// <para><strong>Composite key</strong></para>
/// <para>
///     <see cref="Id"/> is deterministic:
///     <c>"{ClientId}:{TargetType}:{TargetId}:{Granularity}:{SegmentStart:yyyyMMddHH}"</c>.
///     Legacy pre-segmentation documents use the shorter form without the time suffix and
///     have a <c>null</c> <see cref="SegmentStart"/>; they expire naturally via retention.
/// </para>
/// </summary>
public record UsageSnapshot
{
    /// <summary>
    /// A unique identifier for this usage snapshot document.
    /// <br></br>
    /// Segmented format: <c>"{ClientId}:{TargetType}:{TargetId}:{Granularity}:{SegmentStart:yyyyMMddHH}"</c>.
    /// Legacy format (pre-segmentation): <c>"{ClientId}:{TargetType}:{TargetId}:{Granularity}"</c>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// ID of the client this usage data belongs to.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// ID of the service or resource pool being tracked.
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>
    /// The type of target this usage data is tracking.
    /// </summary>
    public TargetType TargetType { get; init; }

    /// <summary>
    /// The "exactness" of data in this snapshot - how aggregated are the time buckets in the snapshot.
    /// </summary>
    public BucketGranularity Granularity { get; init; }

    /// <summary>
    /// The start of the time segment this document covers.
    ///
    /// <para>
    ///     Each segment spans a fixed window determined by the <see cref="Granularity"/>:
    ///     <see cref="BucketGranularity.Second"/> → 1 hour,
    ///     <see cref="BucketGranularity.FiveMinute"/> → 1 day,
    ///     <see cref="BucketGranularity.Hour"/> → 1 week,
    ///     <see cref="BucketGranularity.Day"/> → 1 month.
    ///     All <see cref="Buckets"/> in this document fall within
    ///     <c>[SegmentStart, SegmentStart + window)</c>.
    /// </para>
    /// <para>
    ///     <c>null</c> for legacy pre-segmentation documents that will age out naturally
    ///     via the retention pruning cycle.
    /// </para>
    /// </summary>
    public DateTime? SegmentStart { get; init; }

    /// <summary>
    /// Ordered list of usage aggregations over time, oldest first.
    /// </summary>
    public List<UsageBucket> Buckets { get; init; } = [];
}
