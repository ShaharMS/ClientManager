using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IResourceAllocationDatabase"/>.
/// Stores allocations in <see cref="IDocumentStore"/> and maintains atomic counters
/// for active-count queries to avoid full-collection scans on the acquire hot path.
/// </summary>
public class ResourceAllocationDatabase : IResourceAllocationDatabase
{
    private readonly IDocumentStore _store;
    private const string Collection = "ResourceAllocation";
    private static readonly TimeSpan CounterTtl = TimeSpan.FromHours(24);

    /// <summary>
    /// Initializes a new instance of <see cref="ResourceAllocationDatabase"/>.
    /// </summary>
    /// <param name="store">The document store to delegate operations to.</param>
    public ResourceAllocationDatabase(IDocumentStore store)
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
    public async Task<(int PoolCount, int ClientCount)> GetActiveCountsAsync(
        string resourcePoolId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var poolKey = PoolCounterKey(resourcePoolId);
        var clientKey = ClientCounterKey(resourcePoolId, clientId);
        var counts = await _store.GetManyCountersAsync([poolKey, clientKey], cancellationToken);

        return (NormalizeCount(GetCount(counts, poolKey)), NormalizeCount(GetCount(counts, clientKey)));
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, int>> GetActiveCountsByPoolAsync(CancellationToken cancellationToken = default)
    {
        var query = new DocumentQuery()
            .Where(nameof(ResourceAllocation.IsReleased), FilterOperator.Equals, false)
            .Where(nameof(ResourceAllocation.ExpiresAt), FilterOperator.GreaterThan, DateTime.UtcNow);

        var result = await _store.SearchAsync<ResourceAllocation>(Collection, query, cancellationToken);
        return result.Items
            .GroupBy(a => a.ResourcePoolId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <inheritdoc />
    public async Task<Dictionary<(string PoolId, string ClientId), int>> GetActiveCountsByPoolAndClientAsync(CancellationToken cancellationToken = default)
    {
        var query = new DocumentQuery()
            .Where(nameof(ResourceAllocation.IsReleased), FilterOperator.Equals, false)
            .Where(nameof(ResourceAllocation.ExpiresAt), FilterOperator.GreaterThan, DateTime.UtcNow);

        var result = await _store.SearchAsync<ResourceAllocation>(Collection, query, cancellationToken);
        return result.Items
            .GroupBy(a => (a.ResourcePoolId, a.ClientId))
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <inheritdoc />
    public async Task CreateAsync(ResourceAllocation allocation, CancellationToken cancellationToken = default)
    {
        await _store.SetAsync(Collection, allocation.Id, allocation, cancellationToken);
        await IncrementAllocationCountersAsync(allocation, cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkReleasedAsync(string allocationId, CancellationToken cancellationToken = default)
    {
        var allocation = await GetByIdAsync(allocationId, cancellationToken);
        if (allocation is null)
            return;

        await MarkReleasedAsync(allocation, cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkReleasedAsync(ResourceAllocation allocation, CancellationToken cancellationToken = default)
    {
        if (allocation.IsReleased)
            return;

        await _store.SetAsync(Collection, allocation.Id, allocation with { IsReleased = true }, cancellationToken);
        await DecrementAllocationCountersAsync(allocation, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var query = new DocumentQuery()
            .Where(nameof(ResourceAllocation.IsReleased), FilterOperator.Equals, false);

        var result = await _store.SearchAsync<ResourceAllocation>(Collection, query, cancellationToken);
        var now = DateTime.UtcNow;
        var counterDecrements = new Dictionary<string, long>(StringComparer.Ordinal);
        var count = 0;

        foreach (var allocation in result.Items)
        {
            if (allocation.ExpiresAt > now)
                continue;

            await _store.SetAsync(Collection, allocation.Id, allocation with { IsReleased = true }, cancellationToken);
            AddCounterDelta(counterDecrements, PoolCounterKey(allocation.ResourcePoolId), 1);
            AddCounterDelta(counterDecrements, ClientCounterKey(allocation.ResourcePoolId, allocation.ClientId), 1);
            count++;
        }

        await _store.DecrementManyCountersAsync(counterDecrements, cancellationToken);

        return count;
    }

    /// <inheritdoc />
    public async Task ReconcileCountersAsync(CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<ResourceAllocation>(Collection, cancellationToken);
        var now = DateTime.UtcNow;

        var counterValues = new Dictionary<string, (long value, TimeSpan window)>(StringComparer.Ordinal);

        foreach (var allocation in all)
        {
            if (allocation.IsReleased || allocation.ExpiresAt <= now)
                continue;

            var poolKey = PoolCounterKey(allocation.ResourcePoolId);
            AddCounterValue(counterValues, poolKey);

            var clientKey = ClientCounterKey(allocation.ResourcePoolId, allocation.ClientId);
            AddCounterValue(counterValues, clientKey);
        }

        await _store.SetManyCountersAsync(counterValues, cancellationToken);
    }

    private Task IncrementAllocationCountersAsync(ResourceAllocation allocation, CancellationToken cancellationToken)
    {
        return _store.IncrementManyCountersAsync(new Dictionary<string, (long amount, TimeSpan window)>
        {
            [PoolCounterKey(allocation.ResourcePoolId)] = (1, CounterTtl),
            [ClientCounterKey(allocation.ResourcePoolId, allocation.ClientId)] = (1, CounterTtl)
        }, cancellationToken);
    }

    private Task DecrementAllocationCountersAsync(ResourceAllocation allocation, CancellationToken cancellationToken)
    {
        return _store.DecrementManyCountersAsync(new Dictionary<string, long>
        {
            [PoolCounterKey(allocation.ResourcePoolId)] = 1,
            [ClientCounterKey(allocation.ResourcePoolId, allocation.ClientId)] = 1
        }, cancellationToken);
    }

    private static void AddCounterDelta(IDictionary<string, long> counters, string key, long amount)
    {
        var current = counters.TryGetValue(key, out var value) ? value : 0;
        counters[key] = current + amount;
    }

    private static void AddCounterValue(IDictionary<string, (long value, TimeSpan window)> counters, string key)
    {
        var current = counters.TryGetValue(key, out var entry) ? entry.value : 0;
        counters[key] = (current + 1, CounterTtl);
    }

    private static long GetCount(IReadOnlyDictionary<string, long> counts, string key)
    {
        return counts.TryGetValue(key, out var count) ? count : 0;
    }

    private static int NormalizeCount(long count) => (int)Math.Max(0, count);

    private static string PoolCounterKey(string poolId) => $"alloc-count:pool:{poolId}";
    private static string ClientCounterKey(string poolId, string clientId) => $"alloc-count:client:{poolId}:{clientId}";
}
