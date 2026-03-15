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
    /// Counts requests in a continuously sliding time window.
    /// </summary>
    SlidingWindow,

    /// <summary>
    /// Uses a token bucket that refills at a configured rate, allowing controlled bursts.
    /// </summary>
    TokenBucket
}
