namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Defines the algorithm used to enforce rate limits.
/// </summary>
public enum RateLimitStrategy
{
    /// <summary>
    /// Counts requests in fixed, non-overlapping time windows.
    /// </summary>
    FixedWindow,

    /// <summary>
    /// Approximate sliding window using a weighted average of two adjacent fixed windows.
    /// Unlike a true sliding log (which stores every request timestamp), this approach
    /// maintains only two counters and blends the previous window's count with the current
    /// window's count proportionally to how far into the current window the request falls.
    /// This is memory-efficient and widely used (e.g. Redis, Cloudflare) but may slightly
    /// over- or under-count near window boundaries.
    /// </summary>
    ApproximateSlidingWindow,

    /// <summary>
    /// Uses a token bucket that refills at a configured rate, allowing controlled bursts.
    /// </summary>
    TokenBucket
}
