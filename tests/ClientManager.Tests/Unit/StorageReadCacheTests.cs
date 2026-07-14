using ClientManager.Api.Services.Storage;
using ClientManager.Api.Services.Storage.Instrumentation;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ClientManager.Tests.Unit;

public sealed class StorageReadCacheTests
{
    private static StorageReadCache CreateCache(MemoryCache memory, StorageReadCacheOptions? options = null) =>
        new(memory, Options.Create(options ?? new StorageReadCacheOptions()), new StorageMetrics(new TestMeterFactory()));

    [Fact]
    public async Task InvalidateCatalog_forces_factory_to_run_again()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memory, new StorageReadCacheOptions
        {
            CatalogTtl = TimeSpan.FromMinutes(1),
            HotPathCatalogTtl = TimeSpan.FromSeconds(1),
            HotPathClientServiceTtl = TimeSpan.FromSeconds(5)
        });

        var calls = 0;
        Task<string> Factory(CancellationToken _) => Task.FromResult($"value-{++calls}");

        var first = await cache.GetOrCreateCatalogAsync("clients", Factory, CancellationToken.None);
        var cached = await cache.GetOrCreateCatalogAsync("clients", Factory, CancellationToken.None);
        cache.InvalidateCatalog();
        var afterInvalidation = await cache.GetOrCreateCatalogAsync("clients", Factory, CancellationToken.None);

        Assert.Equal("value-1", first);
        Assert.Equal("value-1", cached);
        Assert.Equal("value-2", afterInvalidation);
    }

    [Fact]
    public async Task HotPathCatalogTtl_can_be_used_for_global_limit_reads()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memory, new StorageReadCacheOptions
        {
            CatalogTtl = TimeSpan.FromMinutes(1),
            HotPathCatalogTtl = TimeSpan.FromMilliseconds(1)
        });

        var calls = 0;
        Task<int> Factory(CancellationToken _) => Task.FromResult(++calls);

        await cache.GetOrCreateCatalogAsync("global-limits", Factory, CancellationToken.None, ttl: TimeSpan.FromMilliseconds(1));
        await Task.Delay(15);
        var second = await cache.GetOrCreateCatalogAsync("global-limits", Factory, CancellationToken.None, ttl: TimeSpan.FromMilliseconds(1));

        Assert.Equal(2, second);
    }
}
