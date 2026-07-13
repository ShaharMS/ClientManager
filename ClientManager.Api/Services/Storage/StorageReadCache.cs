using ClientManager.Shared.Configuration.Storage;
using ClientManager.Api.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Owns storage-side catalog cache entries and invalidation scopes.
/// </summary>
public sealed class StorageReadCache : IStorageReadCache, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly StorageReadCacheOptions _options;
    private readonly object _sync = new();
    private CancellationTokenSource _catalogInvalidation = new();

    public StorageReadCache(IMemoryCache cache, IOptions<StorageReadCacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<T> GetOrCreateCatalogAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken,
        TimeSpan? ttl = null) =>
        GetOrCreateAsync($"catalog:{key}", ttl ?? _options.CatalogTtl, _catalogInvalidation.Token, factory, cancellationToken);

    /// <inheritdoc />
    public void InvalidateCatalog()
    {
        CancellationTokenSource previous;
        lock (_sync)
        {
            previous = _catalogInvalidation;
            _catalogInvalidation = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    public void Dispose() => _catalogInvalidation.Dispose();

    private Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        CancellationToken invalidationToken,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;
            entry.AddExpirationToken(new CancellationChangeToken(invalidationToken));
            return await factory(cancellationToken);
        })!;
}
