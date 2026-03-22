using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Models;

/// <summary>
/// Configuration options for the persistence layer. Bound from the "Persistence" section of appsettings.
/// 
/// Persistent storage in this application 
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
