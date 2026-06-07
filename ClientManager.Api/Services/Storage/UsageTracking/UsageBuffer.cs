using System.Collections.Concurrent;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Storage.UsageTracking;

/// <summary>
/// In-memory aggregation buffer for usage events before persistence.
/// </summary>
public class UsageBuffer
{
    private readonly ConcurrentDictionary<UsageBufferKey, long> _counters = new();

    /// <summary>
    /// Increments the in-memory count for a usage key.
    /// </summary>
    public void Increment(UsageBufferKey key)
    {
        _counters.AddOrUpdate(key, 1, (_, current) => current + 1);
    }

    /// <summary>
    /// Atomically drains the current counters.
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