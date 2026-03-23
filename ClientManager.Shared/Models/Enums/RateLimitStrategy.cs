using System.Text.Json.Serialization;

namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Defines the algorithm used to enforce rate limits.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RateLimitStrategy
{
    /// <summary>
    /// Counts requests in fixed, non-overlapping time windows.
    /// <para>
    ///     Requests are grouped into discrete intervals (e.g. 1 minute), 
    ///     and the count resets at the end of each interval. This is straightforward 
    ///     to implement and efficient but can lead to uneven request distribution, 
    ///     especially if many requests arrive near the end of a window.
    /// </para>
    /// <para>
    ///     For example, with a 1-minute window, all requests from 12:00:00 to 12:00:59 
    ///     are counted together, and the count resets at 12:01:00. This is simple 
    ///     and efficient but can lead to burstiness at window boundaries.
    /// </para>
    /// </summary>
    FixedWindow,

    /// <summary>
    /// Approximate sliding window using a weighted average of two adjacent fixed windows.
    /// <para>
    ///     Unlike a true sliding log (which stores every request timestamp), this approach
    ///     maintains only two counters and blends the previous window's count with the current
    ///     window's count proportionally to how far into the current window the request falls.
    ///     This is memory-efficient and widely used (e.g. Redis, Cloudflare) but may slightly
    ///     over- or under-count near window boundaries.
    /// </para>
    /// <para>
    ///     For example, with a 1-minute window and a request at 12:00:30, the count would be:
    ///     (0.5 * count from 11:59:00-11:59:59) + (0.5 * count from 12:00:00-12:00:29)
    /// </para>
    /// </summary>
    ApproximateSlidingWindow,

    /// <summary>
    /// Uses a token bucket that refills at a configured rate, allowing controlled bursts.
    /// <para>
    ///    Each request consumes a token, and tokens are added to the bucket at a steady rate. 
    ///     If the bucket is empty, requests are denied until tokens are replenished. This allows for more flexible rate limiting, accommodating bursts while enforcing an average rate over time.
    /// </para>
    /// <para>
    ///    For example, with a bucket capacity of 10 tokens and a refill rate of 1 token per second, 
    ///     the bucket starts full with 10 tokens. If 10 requests arrive at once, 
    ///     they consume all tokens and are allowed. If an 11th request arrives immediately after, 
    ///     it is denied until at least one token is refilled (after 1 second).
    /// </para>
    /// </summary>
    TokenBucket
}
