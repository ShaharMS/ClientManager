namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Identifies the storage backend used for persistence.
/// <para>
///     In this context, persistence refers to the ability of multiple instances 
///     of this application to act in unison, sharing the same data and state 
///     across a distributed environment. The choice of persistence provider 
///     determines how data is stored &amp; accessed across instances.
/// </para>
/// </summary>
public enum PersistenceProvider
{
    /// <summary>
    /// File-based JSON storage, intended for local development, or for a shared persistent volume.
    /// </summary>
    JsonFile,

    /// <summary>
    /// A MongoDB document database, intended for production use in a distributed environment.
    /// </summary>
    MongoDb,

    /// <summary>
    /// A Redis in-memory data store, intended for production use where performance is critical and compromises on durability are acceptable.
    /// </summary>
    Redis,

    /// <summary>
    /// A Lucene.NET embedded search index, intended for PVC-based deployments
    /// that need full-text and field-level search without an external database.
    /// </summary>
    Lucene,

    /// <summary>
    /// SQLite storage for usage statistics (local or single-instance deployments).
    /// </summary>
    Sqlite
}

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
