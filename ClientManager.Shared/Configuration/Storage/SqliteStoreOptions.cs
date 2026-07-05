namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// SQLite settings for the Statistics storage role.
/// </summary>
public record SqliteStoreOptions
{
    /// <summary>
    /// Path to the SQLite database file (e.g. <c>./data/statistics.db</c>).
    /// </summary>
    public string DatabasePath { get; set; } = "./data/statistics.db";
}
