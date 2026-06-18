using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Redis-backed leader election using SET NX with lease renewal.
/// </summary>
public sealed class RedisDistributedLeaderLock : IDistributedLeaderLock
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly BackgroundWorkersOptions _options;
    private readonly IAppLogger<RedisDistributedLeaderLock> _logger;
    private readonly string _instanceId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public RedisDistributedLeaderLock(
        IConnectionMultiplexer multiplexer,
        IOptions<BackgroundWorkersOptions> options,
        IAppLogger<RedisDistributedLeaderLock> logger)
    {
        _multiplexer = multiplexer;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IDistributedLeaderLease?> TryAcquireAsync(string workerName, CancellationToken cancellationToken = default)
    {
        var key = LeaderKey(workerName);
        var acquired = await _multiplexer.GetDatabase().StringSetAsync(
            key,
            _instanceId,
            _options.LeaderLeaseDuration,
            When.NotExists);

        if (!acquired)
        {
            return null;
        }

        return new RedisLeaderLease(_multiplexer, key, _instanceId, _options, _logger);
    }

    private static string LeaderKey(string workerName) => $"clientmanager:leader:{workerName}";

    private sealed class RedisLeaderLease : IDistributedLeaderLease
    {
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly string _key;
        private readonly string _instanceId;
        private readonly BackgroundWorkersOptions _options;
        private readonly IAppLogger<RedisDistributedLeaderLock> _logger;
        private bool _released;

        public RedisLeaderLease(
            IConnectionMultiplexer multiplexer,
            string key,
            string instanceId,
            BackgroundWorkersOptions options,
            IAppLogger<RedisDistributedLeaderLock> logger)
        {
            _multiplexer = multiplexer;
            _key = key;
            _instanceId = instanceId;
            _options = options;
            _logger = logger;
        }

        public async Task RenewAsync(CancellationToken cancellationToken = default)
        {
            if (_released)
            {
                return;
            }

            var database = _multiplexer.GetDatabase();
            var current = await database.StringGetAsync(_key);
            if (current != _instanceId)
            {
                _released = true;
                return;
            }

            await database.KeyExpireAsync(_key, _options.LeaderLeaseDuration);
        }

        public async ValueTask DisposeAsync()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            try
            {
                var database = _multiplexer.GetDatabase();
                var current = await database.StringGetAsync(_key);
                if (current == _instanceId)
                {
                    await database.KeyDeleteAsync(_key);
                }
            }
            catch (Exception exception)
            {
                _logger.Warn("Failed to release leader lock", exception: exception);
            }
        }
    }
}

/// <summary>
/// Grants leadership to every instance for single-host deployments.
/// </summary>
public sealed class LocalDistributedLeaderLock : IDistributedLeaderLock
{
    public Task<IDistributedLeaderLease?> TryAcquireAsync(string workerName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IDistributedLeaderLease?>(new LocalLeaderLease());
    }

    private sealed class LocalLeaderLease : IDistributedLeaderLease
    {
        public Task RenewAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
