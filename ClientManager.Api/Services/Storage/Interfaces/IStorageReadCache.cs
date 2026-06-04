namespace ClientManager.Api.Services.Storage.Interfaces;

/// <summary>
/// Coordinates authoritative storage-side caches for catalog and statistics reads.
/// </summary>
public interface IStorageReadCache
{
    Task<T> GetOrCreateCatalogAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken);

    Task<T> GetOrCreateStatisticsAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken);

    void InvalidateCatalog();

    void InvalidateStatistics();
}