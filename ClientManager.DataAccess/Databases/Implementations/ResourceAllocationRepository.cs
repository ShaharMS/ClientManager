using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IResourceAllocationRepository"/>.
/// Stores allocations in <see cref="IDocumentStore"/> and performs filtering in memory.
/// </summary>
public class ResourceAllocationRepository : IResourceAllocationRepository
{
    private readonly IDocumentStore _store;
    private const string Collection = "ResourceAllocation";

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
        var all = await _store.GetAllAsync<ResourceAllocation>(Collection, cancellationToken);
        var now = DateTime.UtcNow;
        return all.Count(a => a.ResourcePoolId == resourcePoolId && !a.IsReleased && a.ExpiresAt > now);
    }

    /// <inheritdoc />
    public async Task<int> GetActiveCountByClientAsync(string resourcePoolId, string clientId, CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<ResourceAllocation>(Collection, cancellationToken);
        var now = DateTime.UtcNow;
        return all.Count(a => a.ResourcePoolId == resourcePoolId && a.ClientId == clientId && !a.IsReleased && a.ExpiresAt > now);
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
    public Task CreateAsync(ResourceAllocation allocation, CancellationToken cancellationToken = default) =>
        _store.SetAsync(Collection, allocation.Id, allocation, cancellationToken);

    /// <inheritdoc />
    public async Task MarkReleasedAsync(string allocationId, CancellationToken cancellationToken = default)
    {
        var allocation = await GetByIdAsync(allocationId, cancellationToken);
        if (allocation is null)
            return;

        await _store.SetAsync(Collection, allocationId, allocation with { IsReleased = true }, cancellationToken);
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
                count++;
            }
        }

        return count;
    }
}
