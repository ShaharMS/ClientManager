# Plan: Hot Path Performance Observability — Step 4: Hot Path Logic

> **Status**: 🔲 Not started
> **Prerequisite**: [hot-path-performance-observability-3-storage-counters.md](hot-path-performance-observability-3-storage-counters.md)
> **Next**: [hot-path-performance-observability-5-verification.md](hot-path-performance-observability-5-verification.md)
> **Parent**: [hot-path-performance-observability-overview.md](hot-path-performance-observability-overview.md)

## TL;DR

Reduce avoidable work in access checks and resource allocation after storage is safe and observable. Focus on removing redundant reads/writes, avoiding duplicate global rate-limit counter operations, and batching capacity checks without changing controller boundaries.

## Reference Pattern

Keep controllers thin and follow existing service/repository responsibilities.

In [AccessCheckController.cs](ClientManager.StorageApi/Controllers/AccessCheckController.cs):
- The controller delegates directly to [AccessControlService.cs](ClientManager.StorageApi/Services/Implementations/AccessControlService.cs).
- Keep this thin-controller shape intact.

In [AccessControlService.cs](ClientManager.StorageApi/Services/Implementations/AccessControlService.cs):
- The access path is currently configuration read, service read, access setting validation, global limit, client limit, record usage.
- Use the spans from Step 2 to verify which segments improve.

In [RateLimitService.cs](ClientManager.StorageApi/Services/Implementations/RateLimiting/RateLimitService.cs):
- Global limits currently evaluate and then peek when a client contributes and is not exempt.
- Client service/global limits are combined after separate evaluations.

In [ResourceAllocationService.cs](ClientManager.StorageApi/Services/Implementations/ResourceAllocationService.cs):
- Acquire currently performs pool read, config read, client capacity check, global limit check, pool capacity check, allocation write, and usage recording.
- Release currently reads an allocation before calling database release logic.

## Steps

### 1. Parallelize independent catalog reads safely

In [AccessControlService.cs](ClientManager.StorageApi/Services/Implementations/AccessControlService.cs), start client configuration and service lookup work together where exception ordering remains acceptable. In [ResourceAllocationService.cs](ClientManager.StorageApi/Services/Implementations/ResourceAllocationService.cs), start pool and client configuration lookups together. Await and validate results in the current deny-by-default order so response behavior remains predictable.

```csharp
var configurationTask = GetConfigurationAsync(clientId, serviceId, cancellationToken);
var serviceTask = GetServiceAsync(serviceId, clientId, cancellationToken);
```

### 2. Avoid duplicate global rate-limit evaluation

In [RateLimitService.cs](ClientManager.StorageApi/Services/Implementations/RateLimiting/RateLimitService.cs), when `contributesToGlobal` is true and `exemptFromGlobal` is false, reuse the result of `EvaluateAsync` instead of calling `PeekAsync` immediately afterward. When the client is exempt, keep the increment if contribution is enabled but return allowed. When contribution is false, keep using peek for enforcement.

### 3. Stop consuming downstream counters after denial

Review the desired semantics in [RateLimitService.cs](ClientManager.StorageApi/Services/Implementations/RateLimiting/RateLimitService.cs): a service-level denial should not consume global client counters unless existing behavior explicitly requires that accounting. Implement early-return semantics for denied stricter limits and document the behavior in service/interface XML docs if changed.

### 4. Batch allocation capacity counter reads

After Step 3 adds batch counter support, update [ResourceAllocationDatabase.cs](ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs) and [ResourceAllocationService.cs](ClientManager.StorageApi/Services/Implementations/ResourceAllocationService.cs) so pool and client active counts are fetched in one storage call for acquire. Preserve the same denial reason ordering: client cap before global rate limit before pool capacity, unless requirements say otherwise.

### 5. Remove redundant release reads

Change release flow so [ResourceAllocationService.cs](ClientManager.StorageApi/Services/Implementations/ResourceAllocationService.cs) does not fetch an allocation and then force [ResourceAllocationDatabase.cs](ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs) to fetch it again. Add a repository/database method that marks a known allocation released or returns enough state from one method call.

```csharp
Task MarkReleasedAsync(ResourceAllocation allocation, CancellationToken cancellationToken = default);
```

### 6. Keep usage recording outside storage critical sections

Verify [UsageRecorder.cs](ClientManager.StorageApi/Services/Implementations/UsageTracking/UsageRecorder.cs) and metric emission remain in-memory work after storage operations complete. Do not add logging or tracing that holds storage locks while writing structured logs.

## Verification

- `dotnet build .\ClientManager.slnx` completes without errors.
- Access-check behavior for allowed, not-configured, disabled-client, disabled-service, global-limit, and client-limit cases matches previous API responses except any documented counter-consumption change.
- Resource acquire behavior for allowed, client-cap, global-pool-limit, and no-slots cases matches previous API responses.
- Resource release no longer performs two allocation reads for a normal release, as shown by traces or storage operation logs.
- Traces show fewer counter operations for global limit checks and token bucket evaluations.
- The hot-path baseline shows lower or equal p95 latency for access checks and acquires compared with the clean Step 1 baseline.
- **UI: Navigate to `/` — verify dashboard totals remain consistent after access and allocation traffic.**
- **UI: Navigate to `/monitor` — verify access and allocation activity appears without chart errors.**
- **UI: Navigate to `/allocations` — perform acquire/release traffic and verify released items do not leave stale active counts.**
