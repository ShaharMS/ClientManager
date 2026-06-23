namespace ClientManager.Shared.Models.Entities;

using ClientManager.Shared.Models.Enums;

public static class UsageBucketExtensions
{
    public static long TotalDenied(this UsageBucket bucket) =>
        bucket.DeniedUnauthenticatedCount
        + bucket.DeniedBlockedCount
        + bucket.DeniedRateLimitedCount
        + bucket.DeniedCapacityLimitedCount;

    /// <summary>Legacy snapshots may only have DeniedCount; attribute remainder to Blocked.</summary>
    public static (long Unauth, long Blocked, long RateLimited, long Capacity) GetDeniedBreakdown(this UsageBucket bucket)
    {
        var unauth = bucket.DeniedUnauthenticatedCount;
        var blocked = bucket.DeniedBlockedCount;
        var rateLimited = bucket.DeniedRateLimitedCount;
        var capacity = bucket.DeniedCapacityLimitedCount;
        var categorized = unauth + blocked + rateLimited + capacity;
        if (categorized < bucket.DeniedCount)
        {
            blocked += bucket.DeniedCount - categorized;
        }

        return (unauth, blocked, rateLimited, capacity);
    }

    public static UsageBucket WithDeniedDelta(UsageBucket bucket, UsageDenialCategory category, long delta) =>
        bucket.WithDeniedDelta(category, delta);
}
