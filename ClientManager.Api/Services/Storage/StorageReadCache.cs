using System.Diagnostics;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Instrumentation;
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
    private readonly StorageMetrics _metrics;
    private readonly object _sync = new();
    private CancellationTokenSource _catalogInvalidation = new();

    public StorageReadCache(IMemoryCache cache, IOptions<StorageReadCacheOptions> options, StorageMetrics metrics)
    {
        _cache = cache;
        _options = options.Value;
        _metrics = metrics;
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

    private async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        CancellationToken invalidationToken,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        if (_cache.TryGetValue(key, out T? cached))
        {
            stopwatch.Stop();
            // ponytail: rare race if another thread just populated the entry — may skip both hit and miss.
            var tags = new TagList { { "result", "hit" } };
            _metrics.CatalogCacheLookups.Add(1, tags);
            _metrics.CatalogCacheDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            return cached!;
        }

        var value = (await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;
            entry.AddExpirationToken(new CancellationChangeToken(invalidationToken));
            _metrics.CatalogCacheLookups.Add(1, new TagList { { "result", "miss" } });
            return await factory(cancellationToken);
        }))!;
        stopwatch.Stop();
        _metrics.CatalogCacheDuration.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList { { "result", "miss" } });
        return value;
    }
}
