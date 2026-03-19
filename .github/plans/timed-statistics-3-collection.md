# Plan: Timed Statistics — Step 3: Collection Layer

> **Status**: 🔲 Not started
> **Prerequisite**: [timed-statistics-2-data-layer.md](timed-statistics-2-data-layer.md)
> **Next**: [timed-statistics-4-event-integration.md](timed-statistics-4-event-integration.md)
> **Parent**: [timed-statistics-overview.md](timed-statistics-overview.md)

## TL;DR

Create the in-memory `UsageBuffer` that accumulates event counts with zero latency impact, and the `IUsageRecorder`/`UsageRecorder` service that provides the API for recording usage events. These are called from the hot path (access checks, resource allocation) and must be lock-free.

## Reference Pattern

In [ClientManager.Api/Services/Instrumentation/ClientManagerMetrics.cs](ClientManager.Api/Services/Instrumentation/ClientManagerMetrics.cs):
- Singleton service registered in DI
- Provides instrumentation methods called from business logic services
- Uses `Counter<long>` pattern — our buffer uses `ConcurrentDictionary` with `Interlocked` increments

## Steps

### 1. Create `UsageBufferKey` record

File: `ClientManager.Api/Services/UsageTracking/UsageBufferKey.cs`

```csharp
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.UsageTracking;

/// <summary>
/// Composite key identifying a specific usage metric in the in-memory buffer.
/// </summary>
public record UsageBufferKey(
    string ClientId,
    GlobalRateLimitTarget TargetType,
    string TargetId,
    UsageEventType EventType);
```

### 2. Create `UsageBuffer` class

File: `ClientManager.Api/Services/UsageTracking/UsageBuffer.cs`

```csharp
using System.Collections.Concurrent;

namespace ClientManager.Api.Services.UsageTracking;

/// <summary>
/// Thread-safe in-memory buffer that accumulates usage event counts.
/// Designed for high-throughput, lock-free increments on the hot path.
/// The background persistence service periodically drains this buffer.
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
```

### 3. Create `IUsageRecorder` interface

File: `ClientManager.Api/Interfaces/IUsageRecorder.cs`

```csharp
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Interfaces;

/// <summary>
/// Records usage events (requests, allocations) for historical tracking.
/// Implementations buffer events in memory for periodic persistence.
/// </summary>
public interface IUsageRecorder
{
    /// <summary>
    /// Records a service request event (granted or denied) for a client.
    /// </summary>
    void RecordServiceRequest(string clientId, string serviceId, UsageEventType eventType);

    /// <summary>
    /// Records a resource pool allocation event (granted or denied) for a client.
    /// </summary>
    void RecordAllocationEvent(string clientId, string resourcePoolId, UsageEventType eventType);
}
```

### 4. Create `UsageRecorder` implementation

File: `ClientManager.Api/Services/UsageTracking/UsageRecorder.cs`

```csharp
using ClientManager.Api.Interfaces;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.UsageTracking;

/// <summary>
/// Records usage events by incrementing counters in the <see cref="UsageBuffer"/>.
/// </summary>
public class UsageRecorder : IUsageRecorder
{
    private readonly UsageBuffer _buffer;

    public UsageRecorder(UsageBuffer buffer)
    {
        _buffer = buffer;
    }

    public void RecordServiceRequest(string clientId, string serviceId, UsageEventType eventType)
    {
        var key = new UsageBufferKey(clientId, GlobalRateLimitTarget.Service, serviceId, eventType);
        _buffer.Increment(key);
    }

    public void RecordAllocationEvent(string clientId, string resourcePoolId, UsageEventType eventType)
    {
        var key = new UsageBufferKey(clientId, GlobalRateLimitTarget.ResourcePool, resourcePoolId, eventType);
        _buffer.Increment(key);
    }
}
```

### 5. Register in DI

In [ClientManager.Api/Extensions/ServiceCollectionExtensions.cs](ClientManager.Api/Extensions/ServiceCollectionExtensions.cs), add to a new or existing registration method:

```csharp
services.AddSingleton<UsageBuffer>();
services.AddSingleton<IUsageRecorder, UsageRecorder>();
```

Both must be **Singleton** — the buffer is shared application-wide and the recorder is stateless.

## Verification

- Solution compiles without errors
- `UsageBuffer.Increment` and `UsageBuffer.Drain` work correctly under concurrent access
- `IUsageRecorder` is resolvable from DI
- Calling `RecordServiceRequest` / `RecordAllocationEvent` increments the buffer without blocking
- `Drain()` returns all accumulated counts and resets the buffer to empty
