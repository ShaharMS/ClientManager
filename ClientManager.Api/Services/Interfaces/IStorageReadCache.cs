namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Coordinates authoritative storage-side caches for catalog and statistics reads.
/// </summary>
public interface IStorageReadCache
{
    Task<T> GetOrCreateCatalogAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken,
        TimeSpan? ttl = null);

    Task<T> GetOrCreateStatisticsTailAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken);

    Task<T> GetOrCreateStatisticsClosedAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken);

    void InvalidateCatalog();

    void InvalidateStatistics();

    void InvalidateStatisticsTail();

    void InvalidateStatisticsClosed();
}
