namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Invalidates the local catalog read cache after writes. Other pods converge via configurable catalog TTL.
/// </summary>
public interface ICrossPodCacheInvalidator
{
    /// <summary>
    /// Clears the local catalog cache after a write. Other instances refresh on the next read after <c>StorageReadCache:CatalogTtl</c>.
    /// </summary>
    void PublishCatalogInvalidation();
}
