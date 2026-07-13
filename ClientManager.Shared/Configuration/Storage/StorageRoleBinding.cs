using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Maps a single <see cref="StorageRole"/> to a storage provider and its connection settings.
/// </summary>
/// <remarks>
/// <para>
/// Exactly one of <see cref="MongoDb"/> or <see cref="Redis"/> should be populated, matching
/// the value of <see cref="Provider"/>.
/// </para>
/// <para>
/// When a role appears in <see cref="PersistenceOptions.Roles"/>, this binding overrides
/// <see cref="PersistenceOptions.DefaultProvider"/> and the default connection settings for
/// that role only.
/// </para>
/// </remarks>
public record StorageRoleBinding
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
}
