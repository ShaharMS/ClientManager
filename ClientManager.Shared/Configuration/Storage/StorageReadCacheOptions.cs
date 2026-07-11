namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Configures in-memory read-cache TTLs for catalog and statistics queries.
/// </summary>
/// <remarks>
/// <para>
/// Bind from <c>DangerZone:StorageReadCache</c> (not a top-level section). When the subsection
/// is omitted, property defaults apply and caching still runs.
/// </para>
/// <para>
/// Writes on the local pod invalidate cache immediately. TTLs bound how long <em>other</em> pods
/// may serve stale catalog, hot-path global limits, or closed statistics after a remote change.
/// </para>
/// <para>
/// Automatic invalidation on catalog writes and rollup/prune cycles is code-driven and not configurable here.
/// </para>
/// </remarks>
public sealed class StorageReadCacheOptions
{
    /// <summary>
    /// Cache lifetime for configuration catalog reads (clients, services, pools, global limit rules).
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
    /// Cache lifetime for statistics closed-base reads and exporter queries.
    /// </summary>
    /// <remarks>
    /// <para>Default: 5 seconds.</para>
    /// <para>
    /// Interacts with the statistics closed invalidation scope (rollup/prune on the slow usage loop).
    /// Live dashboard tails use a per-request overlay and are not fully governed by this TTL alone.
    /// </para>
    /// </remarks>
    public TimeSpan StatisticsTtl { get; init; } = TimeSpan.FromSeconds(5);
}
