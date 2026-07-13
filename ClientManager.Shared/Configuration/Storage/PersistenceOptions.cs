using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Configuration options for the persistence layer. Bound from the <c>"Persistence"</c> section.
/// </summary>
public record PersistenceOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Persistence";

    /// <summary>
    /// The fallback storage backend used for roles without an explicit <see cref="Roles"/> entry.
    /// </summary>
    public PersistenceProvider DefaultProvider { get; set; } = PersistenceProvider.Redis;

    /// <summary>
    /// Default MongoDB settings for roles using <see cref="PersistenceProvider.MongoDb"/>.
    /// </summary>
    public MongoDbStoreOptions? DefaultMongoDb { get; set; }

    /// <summary>
    /// Default Redis settings for roles using <see cref="PersistenceProvider.Redis"/>.
    /// </summary>
    public RedisStoreOptions? DefaultRedis { get; set; }

    /// <summary>
    /// Optional per-role overrides.
    /// </summary>
    public Dictionary<StorageRole, StorageRoleBinding>? Roles { get; set; }
}
