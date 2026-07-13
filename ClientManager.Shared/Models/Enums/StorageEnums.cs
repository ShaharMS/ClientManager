namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Identifies the storage backend used for persistence.
/// </summary>
/// <remarks>
/// <para>
/// In this context, persistence refers to the ability of multiple application instances to
/// share the same data and state across a distributed environment. The choice of storage
/// provider determines how documents and counters are stored and accessed.
/// </para>
/// </remarks>
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
/// Identifies a logical storage domain. Each role can be independently mapped to a
/// <see cref="PersistenceProvider"/>, allowing mixed-backend deployments
/// (for example Redis for rate-limit counters and MongoDB for catalog documents).
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
    /// Global RPM second-bucket ring counters used by dashboard statistics.
    /// </summary>
    Rpm
}
