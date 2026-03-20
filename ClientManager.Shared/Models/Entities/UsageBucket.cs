namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// A single time bucket containing granted and denied counts for a usage metric.
/// </summary>
public record UsageBucket
{
    /// <summary>
    /// UTC start of this time bucket.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Number of granted requests or successful acquisitions in this bucket.
    /// </summary>
    public long GrantedCount { get; init; }

    /// <summary>
    /// Number of denied requests or denied acquisitions in this bucket.
    /// </summary>
    public long DeniedCount { get; init; }

    /// <summary>
    /// Number of explicit resource releases in this bucket.
    /// </summary>
    public long ReleasedCount { get; init; }

    /// <summary>
    /// Snapshot of concurrent active allocations at the time this bucket was recorded.
    /// Only populated for resource pool targets.
    /// </summary>
    public long ActiveCount { get; init; }
}
