using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Maps a single <see cref="StorageRole"/> to a specific <see cref="PersistenceProvider"/>
/// and its platform-specific connection settings.
/// <para>
/// Exactly one of <see cref="MongoDb"/>, <see cref="Redis"/>, <see cref="JsonFile"/>,
/// or <see cref="Lucene"/> should be populated, matching the value of
/// <see cref="Provider"/>.
/// </para>
/// </summary>
public class StorageRoleBinding
{
    /// <summary>
    /// The storage backend to use for this role.
    /// </summary>
    public PersistenceProvider Provider { get; set; }

    /// <summary>
    /// MongoDB connection settings. Populated when <see cref="Provider"/> is
    /// <see cref="PersistenceProvider.MongoDb"/>.
    /// </summary>
    public MongoDbStoreOptions? MongoDb { get; set; }

    /// <summary>
    /// Redis connection settings. Populated when <see cref="Provider"/> is
    /// <see cref="PersistenceProvider.Redis"/>.
    /// </summary>
    public RedisStoreOptions? Redis { get; set; }

    /// <summary>
    /// JSON file storage settings. Populated when <see cref="Provider"/> is
    /// <see cref="PersistenceProvider.JsonFile"/>.
    /// </summary>
    public JsonFileStoreOptions? JsonFile { get; set; }

    /// <summary>
    /// Lucene.NET index settings. Populated when <see cref="Provider"/> is
    /// <see cref="PersistenceProvider.Lucene"/>.
    /// </summary>
    public LuceneStoreOptions? Lucene { get; set; }
}