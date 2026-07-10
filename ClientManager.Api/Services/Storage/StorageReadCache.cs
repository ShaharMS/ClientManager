using ClientManager.Shared.Configuration.Storage;
using ClientManager.Api.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Owns storage-side cache entries and invalidation scopes for read-mostly queries.
/// </summary>
public sealed class StorageReadCache : IStorageReadCache, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly StorageReadCacheOptions _options;
    private readonly object _sync = new();
    private CancellationTokenSource _catalogInvalidation = new();
    private CancellationTokenSource _statisticsTailInvalidation = new();
    private CancellationTokenSource _statisticsClosedInvalidation = new();

    public StorageReadCache(IMemoryCache cache, IOptions<StorageReadCacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public Task<T> GetOrCreateCatalogAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken,
        TimeSpan? ttl = null) =>
        GetOrCreateAsync($"catalog:{key}", ttl ?? _options.CatalogTtl, _catalogInvalidation.Token, factory, cancellationToken);

    public Task<T> GetOrCreateStatisticsTailAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken) =>
        GetOrCreateAsync($"statistics:tail:{key}", _options.StatisticsTtl, _statisticsTailInvalidation.Token, factory, cancellationToken);

    public Task<T> GetOrCreateStatisticsClosedAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken) =>
        GetOrCreateAsync($"statistics:closed:{key}", _options.StatisticsTtl, _statisticsClosedInvalidation.Token, factory, cancellationToken);

    public void InvalidateCatalog()
    {
        Rotate(ref _catalogInvalidation);
        InvalidateStatistics();
    }

    public void InvalidateStatistics()
    {
        InvalidateStatisticsTail();
        InvalidateStatisticsClosed();
    }

    public void InvalidateStatisticsTail() => Rotate(ref _statisticsTailInvalidation);

    public void InvalidateStatisticsClosed() => Rotate(ref _statisticsClosedInvalidation);

    public void Dispose()
    {
        _catalogInvalidation.Dispose();
        _statisticsTailInvalidation.Dispose();
        _statisticsClosedInvalidation.Dispose();
    }

    private Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        CancellationToken invalidationToken,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        return _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;
            entry.AddExpirationToken(new CancellationChangeToken(invalidationToken));
            return await factory(cancellationToken);
        })!;
    }

    private void Rotate(ref CancellationTokenSource source)
    {
        CancellationTokenSource previous;
        lock (_sync)
        {
            previous = source;
            source = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }
}
