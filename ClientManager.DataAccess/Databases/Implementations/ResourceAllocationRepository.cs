using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IResourceAllocationRepository"/>.
/// Stores allocations in <see cref="IDocumentStore"/> and maintains atomic counters
/// for active-count queries to avoid full-collection scans on the acquire hot path.
/// </summary>
public class ResourceAllocationRepository : IResourceAllocationRepository
{
    private readonly IDocumentStore _store;
    private const string Collection = "ResourceAllocation";
    private static readonly TimeSpan CounterTtl = TimeSpan.FromHours(24);

    /// <summary>
    /// Initializes a new instance of <see cref="ResourceAllocationRepository"/>.
    /// </summary>
    /// <param name="store">The document store to delegate operations to.</param>
    public ResourceAllocationRepository(IDocumentStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<ResourceAllocation?> GetByIdAsync(string allocationId, CancellationToken cancellationToken = default) =>
        _store.GetAsync<ResourceAllocation>(Collection, allocationId, cancellationToken);

    /// <inheritdoc />
    public async Task<int> GetActiveCountAsync(string resourcePoolId, CancellationToken cancellationToken = default)
    {
        var count = await _store.GetCounterAsync(PoolCounterKey(resourcePoolId), cancellationToken);
        return (int)Math.Max(0, count);
    }

    /// <inheritdoc />
    public async Task<int> GetActiveCountByClientAsync(string resourcePoolId, string clientId, CancellationToken cancellationToken = default)
    {
        var count = await _store.GetCounterAsync(ClientCounterKey(resourcePoolId, clientId), cancellationToken);
        return (int)Math.Max(0, count);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, int>> GetActiveCountsByPoolAsync(CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<ResourceAllocation>(Collection, cancellationToken);
        var now = DateTime.UtcNow;
        return all
            .Where(a => !a.IsReleased && a.ExpiresAt > now)
            .GroupBy(a => a.ResourcePoolId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <inheritdoc />
    public async Task<Dictionary<(string PoolId, string ClientId), int>> GetActiveCountsByPoolAndClientAsync(CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<ResourceAllocation>(Collection, cancellationToken);
        var now = DateTime.UtcNow;
        return all
            .Where(a => !a.IsReleased && a.ExpiresAt > now)
            .GroupBy(a => (a.ResourcePoolId, a.ClientId))
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <inheritdoc />
    public async Task CreateAsync(ResourceAllocation allocation, CancellationToken cancellationToken = default)
    {
        await _store.SetAsync(Collection, allocation.Id, allocation, cancellationToken);
        await _store.IncrementCounterAsync(PoolCounterKey(allocation.ResourcePoolId), CounterTtl, cancellationToken);
        await _store.IncrementCounterAsync(ClientCounterKey(allocation.ResourcePoolId, allocation.ClientId), CounterTtl, cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkReleasedAsync(string allocationId, CancellationToken cancellationToken = default)
    {
        var allocation = await GetByIdAsync(allocationId, cancellationToken);
        if (allocation is null)
            return;

        await _store.SetAsync(Collection, allocationId, allocation with { IsReleased = true }, cancellationToken);
        await _store.DecrementCounterAsync(PoolCounterKey(allocation.ResourcePoolId), cancellationToken);
        await _store.DecrementCounterAsync(ClientCounterKey(allocation.ResourcePoolId, allocation.ClientId), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<ResourceAllocation>(Collection, cancellationToken);
        var now = DateTime.UtcNow;
        var count = 0;

        foreach (var allocation in all)
        {
            if (!allocation.IsReleased && allocation.ExpiresAt <= now)
            {
                await _store.SetAsync(Collection, allocation.Id, allocation with { IsReleased = true }, cancellationToken);
                await _store.DecrementCounterAsync(PoolCounterKey(allocation.ResourcePoolId), cancellationToken);
                await _store.DecrementCounterAsync(ClientCounterKey(allocation.ResourcePoolId, allocation.ClientId), cancellationToken);
                count++;
            }
        }

        return count;
    }

    /// <inheritdoc />
    public async Task ReconcileCountersAsync(CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<ResourceAllocation>(Collection, cancellationToken);
        var now = DateTime.UtcNow;

        var poolCounts = new Dictionary<string, long>();
        var clientCounts = new Dictionary<string, long>();

        foreach (var allocation in all)
        {
            if (allocation.IsReleased || allocation.ExpiresAt <= now)
                continue;

            var poolKey = PoolCounterKey(allocation.ResourcePoolId);
            poolCounts[poolKey] = poolCounts.GetValueOrDefault(poolKey) + 1;

            var clientKey = ClientCounterKey(allocation.ResourcePoolId, allocation.ClientId);
            clientCounts[clientKey] = clientCounts.GetValueOrDefault(clientKey) + 1;
        }

        foreach (var (key, value) in poolCounts)
        {
            await _store.SetCounterAsync(key, value, CounterTtl, cancellationToken);
        }

        foreach (var (key, value) in clientCounts)
        {
            await _store.SetCounterAsync(key, value, CounterTtl, cancellationToken);
        }
    }

    private static string PoolCounterKey(string poolId) => $"alloc-count:pool:{poolId}";
    private static string ClientCounterKey(string poolId, string clientId) => $"alloc-count:client:{poolId}:{clientId}";
}
