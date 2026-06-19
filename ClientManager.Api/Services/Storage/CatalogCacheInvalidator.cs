using ClientManager.Api.Services.Interfaces;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Clears the local catalog read cache after writes. Other pods converge via <see cref="Shared.Configuration.Storage.StorageReadCacheOptions.CatalogTtl"/>.
/// </summary>
public sealed class CatalogCacheInvalidator : ICrossPodCacheInvalidator
{
    private readonly IStorageReadCache _cache;

    public CatalogCacheInvalidator(IStorageReadCache cache)
    {
        _cache = cache;
    }

    public void PublishCatalogInvalidation()
    {
        _cache.InvalidateCatalog();
    }
}
