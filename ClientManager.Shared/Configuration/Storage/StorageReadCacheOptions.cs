namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Configures in-memory read-cache TTLs for catalog queries.
/// </summary>
/// <remarks>
/// <para>
/// Bind from the <c>StorageReadCache</c> configuration section. When the section is omitted,
/// property defaults apply and caching still runs.
/// </para>
/// <para>
/// Writes on the local pod invalidate cache immediately. TTLs bound how long <em>other</em> pods
/// may serve stale catalog entries after a remote change.
/// </para>
/// <para>
/// Catalog invalidation after Admin UI writes is code-driven and not configurable here.
/// </para>
/// </remarks>
public sealed class StorageReadCacheOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "StorageReadCache";

    /// <summary>
    /// Cache lifetime for configuration catalog reads (clients, services, global limits).
    /// </summary>
    /// <remarks>
    /// <para>Default: 30 seconds.</para>
    /// <para>
    /// Higher values reduce storage load but widen cross-pod staleness after Admin UI edits.
    /// Lower values increase load but tighten consistency across replicas.
    /// </para>
    /// </remarks>
    public TimeSpan CatalogTtl { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Cache lifetime for global-limit rule lookups on the access-check hot path.
    /// </summary>
    /// <remarks>
    /// <para>Default: 1 second.</para>
    /// <para>
    /// This TTL directly affects how quickly rate-limit rule changes propagate on
    /// <c>GET /api/v1/access/check</c> in multi-pod deployments. Keep short in production unless
    /// storage load dominates.
    /// </para>
    /// </remarks>
    public TimeSpan HotPathCatalogTtl { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Cache lifetime for client-configuration and service lookups on the access-check hot path.
    /// </summary>
    /// <remarks>
    /// <para>Default: 5 seconds.</para>
    /// <para>
    /// Shares cache keys with catalog <c>GetById</c> reads so local Admin UI writes still
    /// invalidate through the catalog cache invalidation hook.
    /// </para>
    /// </remarks>
    public TimeSpan HotPathClientServiceTtl { get; init; } = TimeSpan.FromSeconds(5);
}
