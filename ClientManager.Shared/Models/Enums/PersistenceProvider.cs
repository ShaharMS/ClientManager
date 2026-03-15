namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Identifies the storage backend used for persistence.
/// </summary>
public enum PersistenceProvider
{
    /// <summary>
    /// File-based JSON storage, intended for local development.
    /// </summary>
    JsonFile,

    /// <summary>
    /// MongoDB document database.
    /// </summary>
    MongoDb,

    /// <summary>
    /// Redis in-memory data store.
    /// </summary>
    Redis
}
