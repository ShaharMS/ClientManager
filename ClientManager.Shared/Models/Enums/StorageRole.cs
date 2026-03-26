namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Identifies a logical storage domain. Each role can be independently mapped to a
/// different <see cref="PersistenceProvider"/>, allowing mixed-backend deployments
/// (e.g. Redis for rate-limit state, MongoDB for statistics).
/// </summary>
public enum StorageRole
{
    /// <summary>
    /// Client configurations, services, resource pools, and global rate limits.
    /// </summary>
    Configuration,

    /// <summary>
    /// Rate-limit state counters (fixed window, sliding window, token bucket).
    /// </summary>
    RateLimiting,

    /// <summary>
    /// Resource allocation documents and their maintained atomic counters.
    /// </summary>
    Allocations,

    /// <summary>
    /// Usage snapshot time-series data.
    /// </summary>
    Statistics
}
