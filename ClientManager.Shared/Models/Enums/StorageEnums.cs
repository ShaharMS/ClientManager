namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Identifies the storage backend used for persistence.
/// </summary>
public enum PersistenceProvider
{
    /// <summary>
    /// A MongoDB document database for distributed deployments.
    /// </summary>
    MongoDb,

    /// <summary>
    /// A Redis data store for distributed deployments where low latency matters.
    /// </summary>
    Redis
}

/// <summary>
/// Identifies a logical storage domain. Each role can be mapped to MongoDB or Redis independently.
/// </summary>
public enum StorageRole
{
    /// <summary>
    /// Client configurations, services, and global rate limits.
    /// </summary>
    Configuration,

    /// <summary>
    /// Rate-limit state counters (fixed window, sliding window, token bucket).
    /// </summary>
    RateLimiting,

    /// <summary>
    /// Global RPM second-bucket ring counters.
    /// </summary>
    Rpm
}
