using System.Collections.Concurrent;

using ClientManager.Api.Models.Entities;

namespace ClientManager.Api.Services.Implementations.UsageTracking;

/// <summary>
/// Thread-safe in-memory buffer that accumulates usage event counts.
/// <para>
/// Designed for high-throughput, lock-free increments on the hot path using
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. The access-check and resource-allocation
/// services call <see cref="Increment"/> synchronously with no I/O overhead. A background
/// service periodically calls <see cref="Drain"/> to atomically snapshot and reset all counters,
/// then flushes the snapshot to persistent usage snapshots.
/// </para>
/// </summary>
public class UsageBuffer
{
    private readonly ConcurrentDictionary<UsageBufferKey, long> _counters = new();

    /// <summary>
    /// Increments the counter for the given key by 1.
    /// </summary>
    public void Increment(UsageBufferKey key)
    {
        _counters.AddOrUpdate(key, 1, (_, current) => current + 1);
    }

    /// <summary>
    /// Atomically drains all accumulated counters, resetting the buffer.
    /// Returns a snapshot of the counts at the time of draining.
    /// </summary>
    public Dictionary<UsageBufferKey, long> Drain()
    {
        var snapshot = new Dictionary<UsageBufferKey, long>();

        foreach (var key in _counters.Keys)
        {
            if (_counters.TryRemove(key, out var count))
            {
                snapshot[key] = count;
            }
        }

        return snapshot;
    }
}
