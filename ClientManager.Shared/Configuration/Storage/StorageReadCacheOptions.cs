namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Configures storage-side cache lifetimes for read-mostly catalog and statistics queries.
/// </summary>
public sealed class StorageReadCacheOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "StorageReadCache";

    /// <summary>
    /// Cache lifetime for configuration catalog reads.
    /// </summary>
    public TimeSpan CatalogTtl { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Cache lifetime for global-limit rule lookups on the access-check hot path.
    /// </summary>
    public TimeSpan HotPathCatalogTtl { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Cache lifetime for statistics and exporter reads.
    /// </summary>
    public TimeSpan StatisticsTtl { get; init; } = TimeSpan.FromSeconds(5);
}
