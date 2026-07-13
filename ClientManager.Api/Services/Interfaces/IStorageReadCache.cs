namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Coordinates the catalog read-through cache for hot management and access-check paths.
/// </summary>
/// <remarks>
/// <para>
/// Catalog documents (clients, services, global limits) are read far more often than they are
/// written. This cache wraps those reads with short TTLs and explicit invalidation so list pages
/// stay responsive without serving stale data after Admin UI edits.
/// </para>
/// <para>
/// Rate-limit enforcement and RPM accounting write directly to storage counters and do not rely on
/// this cache, keeping distributed correctness independent of catalog TTL tuning.
/// </para>
/// <para>
/// <see cref="InvalidateCatalog"/> rotates the catalog invalidation scope. Catalog writes call it
/// after persistence succeeds so subsequent reads rebuild entries from authoritative storage.
/// </para>
/// </remarks>
public interface IStorageReadCache
{
    /// <summary>
    /// Read-through cache for catalog documents (clients, services, global limits).
    /// </summary>
    /// <typeparam name="T">The cached value type.</typeparam>
    /// <param name="key">Logical cache key within the catalog scope.</param>
    /// <param name="factory">Factory that loads the value from storage on a cache miss.</param>
    /// <param name="cancellationToken">Cancels the factory if the backing store is slow or shutting down.</param>
    /// <param name="ttl">Optional entry TTL; uses the configured catalog default when omitted.</param>
    /// <returns>The cached or freshly loaded value.</returns>
    Task<T> GetOrCreateCatalogAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken,
        TimeSpan? ttl = null);

    /// <summary>Rotates the catalog invalidation scope so subsequent reads miss the cache.</summary>
    void InvalidateCatalog();
}
