using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Configuration options for the persistence layer. Bound from the <c>"Persistence"</c>
/// section of <c>appsettings.json</c>.
/// <para>
/// Supports both single-provider and mixed-provider deployments. Set
/// <see cref="DefaultProvider"/> and the matching <c>Default*</c> options block for a
/// uniform backend, or use <see cref="Roles"/> to assign individual
/// <see cref="StorageRole"/> values to different providers.
/// </para>
/// </summary>
public record PersistenceOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Persistence";

    /// <summary>
    /// The fallback storage backend used for any <see cref="StorageRole"/> that does not
    /// have an explicit entry in <see cref="Roles"/>.
    /// </summary>
    public PersistenceProvider DefaultProvider { get; set; } = PersistenceProvider.JsonFile;

    /// <summary>
    /// Default MongoDB settings applied to every role that uses
    /// <see cref="PersistenceProvider.MongoDb"/> unless the role's
    /// <see cref="StorageRoleBinding"/> supplies its own.
    /// </summary>
    public MongoDbStoreOptions? DefaultMongoDb { get; set; }

    /// <summary>
    /// Default Redis settings applied to every role that uses
    /// <see cref="PersistenceProvider.Redis"/> unless the role's
    /// <see cref="StorageRoleBinding"/> supplies its own.
    /// </summary>
    public RedisStoreOptions? DefaultRedis { get; set; }

    /// <summary>
    /// Default JSON file settings applied to every role that uses
    /// <see cref="PersistenceProvider.JsonFile"/> unless the role's
    /// <see cref="StorageRoleBinding"/> supplies its own.
    /// </summary>
    public JsonFileStoreOptions? DefaultJsonFile { get; set; }

    /// <summary>
    /// Default Lucene.NET settings applied to every role that uses
    /// <see cref="PersistenceProvider.Lucene"/> unless the role's
    /// <see cref="StorageRoleBinding"/> supplies its own.
    /// </summary>
    public LuceneStoreOptions? DefaultLucene { get; set; }

    /// <summary>
    /// Default SQLite settings applied to every role that uses
    /// <see cref="PersistenceProvider.Sqlite"/> unless the role's
    /// <see cref="StorageRoleBinding"/> supplies its own.
    /// </summary>
    public SqliteStoreOptions? DefaultSqlite { get; set; }

    /// <summary>
    /// Optional per-role overrides. When a <see cref="StorageRole"/> is present in this
    /// dictionary, its binding takes precedence over <see cref="DefaultProvider"/> and the
    /// <c>Default*</c> options.
    /// </summary>
    public Dictionary<StorageRole, StorageRoleBinding>? Roles { get; set; }
}