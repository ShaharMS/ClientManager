using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Publishes catalog invalidation messages to other API pods via Redis pub/sub.
/// </summary>
public sealed class RedisCacheInvalidationPublisher : ICrossPodCacheInvalidator
{
    private readonly IConnectionMultiplexer? _multiplexer;
    private readonly BackgroundWorkersOptions _options;
    private readonly IStorageReadCache _cache;

    public RedisCacheInvalidationPublisher(
        IStorageReadCache cache,
        IOptions<BackgroundWorkersOptions> options,
        IServiceProvider serviceProvider)
    {
        _cache = cache;
        _options = options.Value;
        _multiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
    }

    public void PublishCatalogInvalidation()
    {
        _cache.InvalidateCatalog();

        if (_multiplexer is null)
        {
            return;
        }

        _ = _multiplexer.GetSubscriber().Publish(
            RedisChannel.Literal(_options.CacheInvalidationChannel),
            "catalog");
    }
}

/// <summary>
/// No-op invalidator for single-host deployments without Redis.
/// </summary>
public sealed class LocalCacheInvalidationPublisher : ICrossPodCacheInvalidator
{
    private readonly IStorageReadCache _cache;

    public LocalCacheInvalidationPublisher(IStorageReadCache cache)
    {
        _cache = cache;
    }

    public void PublishCatalogInvalidation()
    {
        _cache.InvalidateCatalog();
    }
}

/// <summary>
/// Subscribes to Redis invalidation messages and clears the local read cache.
/// </summary>
public sealed class RedisCacheInvalidationSubscriber : BackgroundService
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly BackgroundWorkersOptions _options;
    private readonly IStorageReadCache _cache;
    private readonly IAppLogger<RedisCacheInvalidationSubscriber> _logger;

    public RedisCacheInvalidationSubscriber(
        IConnectionMultiplexer multiplexer,
        IOptions<BackgroundWorkersOptions> options,
        IStorageReadCache cache,
        IAppLogger<RedisCacheInvalidationSubscriber> logger)
    {
        _multiplexer = multiplexer;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _multiplexer.GetSubscriber();
        await subscriber.SubscribeAsync(
            RedisChannel.Literal(_options.CacheInvalidationChannel),
            (_, _) => _cache.InvalidateCatalog());

        _logger.Info("Subscribed to cross-pod cache invalidation channel", new { _options.CacheInvalidationChannel });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
