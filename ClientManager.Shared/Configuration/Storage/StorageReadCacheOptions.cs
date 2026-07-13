namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Configures in-memory read-cache TTLs for catalog queries.
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
    /// Cache lifetime for global-limit lookups on the access-check hot path.
    /// </summary>
    public TimeSpan HotPathCatalogTtl { get; init; } = TimeSpan.FromSeconds(1);
}
