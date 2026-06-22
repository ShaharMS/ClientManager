namespace ClientManager.Shared.Models.Entities;

using ClientManager.Shared.Models.Enums;

/// <summary>
/// A single time-window of aggregated usage counts within a <see cref="UsageSnapshot"/>.
/// The window's duration is implied by the parent snapshot's <see cref="BucketGranularity"/>.
/// </summary>
public record UsageBucket
{
    /// <summary>
    /// UTC start of this time bucket.
    /// <para>
    ///     The bucket covers the half-open interval
    ///     <c>[Timestamp, Timestamp + granularity)</c>. For example, with
    ///     <see cref="BucketGranularity.Hour"/> and a timestamp of
    ///     <c>2024-01-01T14:00:00Z</c>, the bucket covers 14:00:00–14:59:59.
    /// </para>
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Number of <see cref="UsageEventType.Granted"/> events in this window - successful
    /// requests (for services) or successful slot acquisitions (for resource pools).
    /// </summary>
    public long GrantedCount { get; init; }

    public long DeniedUnauthenticatedCount { get; init; }

    public long DeniedBlockedCount { get; init; }

    public long DeniedRateLimitedCount { get; init; }

    public long DeniedCapacityLimitedCount { get; init; }

    /// <summary>
    /// Total denied events in this window (sum of category counters).
    /// </summary>
    public long DeniedCount { get; init; }

    /// <summary>
    /// Number of <see cref="UsageEventType.Released"/> events in this window.
    /// Always <c>0</c> for <see cref="TargetType.Service"/> targets because services are
    /// stateless and have nothing to release.
    /// </summary>
    public long ReleasedCount { get; init; }

    /// <summary>
    /// Point-in-time snapshot of how many resource-pool slots the client held when this
    /// bucket was persisted. Always <c>0</c> for <see cref="TargetType.Service"/> targets.
    ///
    /// <para>
    ///     Unlike the other counters (which are cumulative over the bucket's window), this
    ///     is a gauge captured at flush time. It's useful for observing concurrency trends
    ///     (e.g. "client X consistently holds 8 of its 10 allowed slots").
    /// </para>
    /// </summary>
    public long ActiveCount { get; init; }

    public UsageBucket WithDeniedDelta(UsageDenialCategory category, long delta)
    {
        var unauth = DeniedUnauthenticatedCount;
        var blocked = DeniedBlockedCount;
        var rateLimited = DeniedRateLimitedCount;
        var capacity = DeniedCapacityLimitedCount;

        switch (category)
        {
            case UsageDenialCategory.Unauthenticated:
                unauth += delta;
                break;
            case UsageDenialCategory.Blocked:
                blocked += delta;
                break;
            case UsageDenialCategory.RateLimited:
                rateLimited += delta;
                break;
            case UsageDenialCategory.CapacityLimited:
                capacity += delta;
                break;
        }

        return this with
        {
            DeniedUnauthenticatedCount = unauth,
            DeniedBlockedCount = blocked,
            DeniedRateLimitedCount = rateLimited,
            DeniedCapacityLimitedCount = capacity,
            DeniedCount = unauth + blocked + rateLimited + capacity
        };
    }
}
