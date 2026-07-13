using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Maps a single <see cref="StorageRole"/> to a storage provider and its connection settings.
/// </summary>
public record StorageRoleBinding
{
    /// <summary>
    /// The storage backend to use for this role.
    /// </summary>
    public PersistenceProvider Provider { get; set; }

    /// <summary>
    /// MongoDB connection settings when <see cref="Provider"/> is <see cref="PersistenceProvider.MongoDb"/>.
    /// </summary>
    public MongoDbStoreOptions? MongoDb { get; set; }

    /// <summary>
    /// Redis connection settings when <see cref="Provider"/> is <see cref="PersistenceProvider.Redis"/>.
    /// </summary>
    public RedisStoreOptions? Redis { get; set; }
}
