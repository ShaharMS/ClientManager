namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Coordinates the catalog read-through cache.
/// </summary>
public interface IStorageReadCache
{
    /// <summary>
    /// Read-through cache for catalog documents.
    /// </summary>
    Task<T> GetOrCreateCatalogAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken,
        TimeSpan? ttl = null);

    /// <summary>Rotates the catalog invalidation scope.</summary>
    void InvalidateCatalog();
}
