using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Configuration options for the persistence layer. Bound from the <c>"Persistence"</c> section of <c>appsettings.json</c>.
/// <para>
/// Selects which storage backend (<see cref="PersistenceProvider"/>) is used and provides
/// the connection details for that backend. All repositories and the document store
/// are wired to the chosen provider at startup.
/// </para>
/// </summary>
public class PersistenceOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Persistence";

    /// <summary>
    /// The storage backend to use.
    /// </summary>
    public PersistenceProvider Provider { get; set; } = PersistenceProvider.JsonFile;

    /// <summary>
    /// Directory path for JSON file storage. Only used when <see cref="Provider"/> is <see cref="PersistenceProvider.JsonFile"/>.
    /// </summary>
    public string JsonFileDataDirectory { get; set; } = "./data";

    /// <summary>
    /// MongoDB connection string. Required when <see cref="Provider"/> is <see cref="PersistenceProvider.MongoDb"/>.
    /// </summary>
    public string? MongoDbConnectionString { get; set; }

    /// <summary>
    /// MongoDB database name.
    /// </summary>
    public string MongoDbDatabaseName { get; set; } = "ClientManager";

    /// <summary>
    /// Redis connection string. Required when <see cref="Provider"/> is <see cref="PersistenceProvider.Redis"/>.
    /// </summary>
    public string? RedisConnectionString { get; set; }
}
