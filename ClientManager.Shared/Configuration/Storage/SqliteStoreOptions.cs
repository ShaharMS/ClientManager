namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// SQLite document store settings.
/// </summary>
public record SqliteStoreOptions
{
    /// <summary>
    /// Path to the SQLite database file (e.g. <c>./data/statistics.db</c>).
    /// </summary>
    public string DatabasePath { get; set; } = "./data/store.db";
}
