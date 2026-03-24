namespace ClientManager.Shared.Models.Entities;

using ClientManager.Shared.Models.Enums;

/// <summary>
/// A single time bucket containing statistics for client interactions with one or more targets.
/// </summary>
public record UsageBucket
{
    /// <summary>
    /// UTC start of this time bucket.
    /// <para>
    ///     Used along with the <see cref="BucketGranularity"/> of the parent <see cref="UsageSnapshot"/> to determine the time range this bucket covers. 
    /// </para>
    /// <para>
    ///     For example, if the granularity is 1 hour and the timestamp is <tt>2024-01-01T00:00:00Z</tt>, 
    ///     this bucket would cover the period from <tt>2024-01-01T00:00:00Z</tt> to <tt>2024-01-01T00:59:59Z</tt>.
    /// </para>
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Number of granted requests (for <see cref="TargetType.Service"/>) or successful acquisitions (for <see cref="TargetType.ResourcePool"/>) in this bucket.
    /// </summary>
    public long GrantedCount { get; init; }

    /// <summary>
    /// Number of denied requests (for <see cref="TargetType.Service"/>) or denied acquisitions (for <see cref="TargetType.ResourcePool"/>) in this bucket.
    /// </summary>
    public long DeniedCount { get; init; }

    /// <summary>
    /// Number of explicit resource releases in this bucket.
    /// Relevant only for <see cref="TargetType.ResourcePool"/> targets, and should be <c>0</c> for <see cref="TargetType.Service"/> targets.
    /// </summary>
    public long ReleasedCount { get; init; }

    /// <summary>
    /// Snapshot of concurrent active allocations at the time this bucket was recorded.
    /// Relevant only for <see cref="TargetType.ResourcePool"/> targets, and should be <c>0</c> for <see cref="TargetType.Service"/> targets.
    /// </summary>
    public long ActiveCount { get; init; }
}
