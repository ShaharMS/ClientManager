using ClientManager.Api.Services.Interfaces;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Logging;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Leader election backed by the shared <see cref="IDocumentStore"/> lease API.
/// </summary>
public sealed class StorageBackedLeaderLock : IDistributedLeaderLock
{
    private readonly IDocumentStore _store;
    private readonly BackgroundWorkersOptions _options;
    private readonly IAppLogger<StorageBackedLeaderLock> _logger;
    private readonly string _instanceId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public StorageBackedLeaderLock(
        IDocumentStore store,
        IOptions<BackgroundWorkersOptions> options,
        IAppLogger<StorageBackedLeaderLock> logger)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IDistributedLeaderLease?> TryAcquireAsync(string workerName, CancellationToken cancellationToken = default)
    {
        var key = LeaderKey(workerName);
        var acquired = await _store.TryAcquireLeaseAsync(key, _instanceId, _options.LeaderLeaseDuration, cancellationToken);
        if (!acquired)
        {
            return null;
        }

        return new StorageLeaderLease(_store, key, _instanceId, _options, _logger);
    }

    private static string LeaderKey(string workerName) => $"leader:{workerName}";

    private sealed class StorageLeaderLease : IDistributedLeaderLease
    {
        private readonly IDocumentStore _store;
        private readonly string _key;
        private readonly string _instanceId;
        private readonly BackgroundWorkersOptions _options;
        private readonly IAppLogger<StorageBackedLeaderLock> _logger;
        private bool _released;

        public StorageLeaderLease(
            IDocumentStore store,
            string key,
            string instanceId,
            BackgroundWorkersOptions options,
            IAppLogger<StorageBackedLeaderLock> logger)
        {
            _store = store;
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

            var renewed = await _store.RenewLeaseAsync(_key, _instanceId, _options.LeaderLeaseDuration, cancellationToken);
            if (!renewed)
            {
                _released = true;
            }
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
                await _store.ReleaseLeaseAsync(_key, _instanceId, CancellationToken.None);
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
