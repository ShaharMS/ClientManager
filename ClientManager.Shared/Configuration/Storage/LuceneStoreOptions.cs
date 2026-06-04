namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Settings for Lucene.NET-based storage. Intended for PVC-based deployments that need
/// full-text and field-level search without an external database.
/// <para>
/// These options are not bound independently from configuration. They are referenced
/// either as a default (<see cref="PersistenceOptions.DefaultLucene"/>) or from a
/// per-role <see cref="StorageRoleBinding"/>.
/// </para>
/// </summary>
public record LuceneStoreOptions
{
    /// <summary>
    /// The directory path where Lucene index files are stored.
    /// </summary>
    public string IndexDirectory { get; set; } = "./lucene-index";

    /// <summary>
    /// How often pending writes are committed to disk, in seconds.
    /// </summary>
    public int CommitIntervalSeconds { get; set; } = 1;

    /// <summary>
    /// Maximum number of documents buffered in RAM before an automatic flush.
    /// </summary>
    public int MaxBufferedDocs { get; set; } = 100;

    /// <summary>
    /// RAM buffer size in megabytes for the index writer.
    /// </summary>
    public double RamBufferSizeMb { get; set; } = 16.0;
}