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
}
