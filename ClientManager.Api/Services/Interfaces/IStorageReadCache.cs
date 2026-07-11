namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Coordinates authoritative storage-side caches for catalog and statistics reads.
/// </summary>
/// <remarks>
/// <para>Three independent invalidation scopes exist:</para>
/// <list type="bullet">
/// <item><description><strong>Catalog</strong> — clients, services, pools, global limits. Admin UI writes rotate this scope only.</description></item>
/// <item><description><strong>Statistics closed</strong> — timeseries closed-base aggregates. Rotated on rollup/prune (slow persistence loop).</description></item>
/// <item><description><strong>Statistics tail</strong> — legacy full-response tail entries; retained for API compatibility. Live timeseries freshness no longer depends on tail rotation.</description></item>
/// </list>
/// <para>
/// Catalog invalidation does <em>not</em> cascade into statistics scopes. Statistics reads overlay
/// live counters per request instead of busting cache on every usage flush.
/// </para>
/// </remarks>
public interface IStorageReadCache
{
    /// <summary>
    /// Read-through cache for catalog documents (clients, services, pools, limits).
    /// </summary>
    Task<T> GetOrCreateCatalogAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken,
        TimeSpan? ttl = null);

    /// <summary>
    /// Read-through cache for legacy full timeseries responses that included the live tail in the cached payload.
    /// </summary>
    /// <remarks>Prefer <see cref="GetOrCreateStatisticsClosedAsync{T}"/> plus per-request overlay for new code paths.</remarks>
    Task<T> GetOrCreateStatisticsTailAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Read-through cache for timeseries closed-base aggregates (snapshots without live overlay).
    /// </summary>
    Task<T> GetOrCreateStatisticsClosedAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken);

    /// <summary>Rotates the catalog invalidation scope only.</summary>
    void InvalidateCatalog();

    /// <summary>Rotates both statistics tail and closed invalidation scopes.</summary>
    void InvalidateStatistics();

    /// <summary>Rotates the statistics tail invalidation scope.</summary>
    void InvalidateStatisticsTail();

    /// <summary>Rotates the statistics closed invalidation scope (rollup/prune).</summary>
    void InvalidateStatisticsClosed();
}
