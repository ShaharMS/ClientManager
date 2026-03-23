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
    Redis
}
