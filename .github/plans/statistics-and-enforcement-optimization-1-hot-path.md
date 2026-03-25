# Plan: Statistics and Enforcement Performance Optimization — Step 1: Hot Path

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [statistics-and-enforcement-optimization-2-snapshot-segments.md](statistics-and-enforcement-optimization-2-snapshot-segments.md)
> **Parent**: [statistics-and-enforcement-optimization-overview.md](statistics-and-enforcement-optimization-overview.md)

## TL;DR

The access-check and resource-acquire paths are the most latency-sensitive routes in the system. Currently they perform redundant repository loads (client config loaded 3× per access check), full-collection scans (allocation counts, global rate limit lookups), and excessive state-store round trips (TokenBucket: 4 per evaluation). This step eliminates all of those by passing config forward, maintaining allocation counters atomically, caching global rate limits, and batching state-store operations.

## Reference Pattern

In [../../ClientManager.Api/Services/UsageTracking/UsageBuffer.cs](../../ClientManager.Api/Services/UsageTracking/UsageBuffer.cs):
- Lock-free `ConcurrentDictionary` with `AddOrUpdate` for cheap hot-path mutations.
- Drain is background-only — the request path never blocks on persistence.
- This is the pattern to follow: keep the hot path doing only keyed reads/writes, never scans.

In [../../ClientManager.DataAccess/Databases/Interfaces/IRateLimitStateStore.cs](../../ClientManager.DataAccess/Databases/Interfaces/IRateLimitStateStore.cs):
- Already wraps `IDocumentStore` counter operations 1:1.
- The counter API (`IncrementCounterAsync`, `GetCounterAsync`, `SetCounterAsync`) supports TTL windows — the same API can host allocation counters.

In [../../ClientManager.Api/Services/RateLimiting/FixedWindowStrategy.cs](../../ClientManager.Api/Services/RateLimiting/FixedWindowStrategy.cs):
- Single `IncrementAsync` call per `EvaluateAsync` — this is the ideal: one round trip.
- TokenBucket should approach this efficiency via batched operations.

## Steps

### 1. Pass client config into `RateLimitService` methods instead of re-fetching

Currently `AccessControlService.CheckAccessAsync` loads `ClientConfiguration` at the top, then calls `_rateLimitService.CheckGlobalServiceLimitAsync` and `_rateLimitService.CheckAndIncrementAsync` — both of which call `_clientConfigRepository.GetByIdAsync(clientId)` again internally.

**Change `IRateLimitService`** to add overloads (or replace the existing signatures) that accept the already-loaded `ClientConfiguration`:

```csharp
Task<RateLimitResult> CheckAndIncrementAsync(
    ClientConfiguration config, string serviceId, CancellationToken ct);

Task<RateLimitResult> CheckGlobalServiceLimitAsync(
    ClientConfiguration config, string serviceId, CancellationToken ct);

Task<RateLimitResult> CheckGlobalResourcePoolLimitAsync(
    ClientConfiguration config, string resourcePoolId, CancellationToken ct);
```

**Update `RateLimitService`** to use the passed-in config directly. Remove the config fetch from all three methods. The `CheckWithoutIncrementAsync` and `CheckGlobalAndIncrementAsync` methods still load config themselves since they're called from paths that don't have it preloaded.

**Update `AccessControlService.CheckAccessAsync`** to pass `config` into both rate limit calls.

**Update `ResourceAllocationService.AcquireAsync`** to pass `config` into `CheckGlobalResourcePoolLimitAsync`.

Files to touch:
- `ClientManager.Api/Interfaces/IRateLimitService.cs`
- `ClientManager.Api/Services/RateLimiting/RateLimitService.cs`
- `ClientManager.Api/Services/AccessControlService.cs`
- `ClientManager.Api/Services/ResourceAllocationService.cs`

### 2. Cache global rate limits with short TTL

`GlobalRateLimitRepository.GetByTargetAsync` calls `_store.GetAllAsync<GlobalRateLimit>` and does `FirstOrDefault` — a full collection scan on every access check and every resource acquire. Global rate limits are admin-configured and rarely change.

**Add `IMemoryCache`** to `RateLimitService` (same pattern as `StatisticsService` already uses). Cache the result of `_globalRateLimitRepository.GetByTargetAsync` with a 30-second TTL, keyed by `$"global-limit:{targetId}:{targetType}"`.

Apply this in both `CheckGlobalServiceLimitAsync` and `CheckGlobalResourcePoolLimitAsync`. If a cache miss occurs, fetch from the repository and cache. If the admin updates a global limit, the cache expires within 30 seconds — acceptable for rate limit config.

Files to touch:
- `ClientManager.Api/Services/RateLimiting/RateLimitService.cs` (add `IMemoryCache` constructor parameter, wrap the two `GetByTargetAsync` calls)

### 3. Replace allocation count scans with maintained counters

`ResourceAllocationRepository.GetActiveCountAsync` and `GetActiveCountByClientAsync` both call `_store.GetAllAsync<ResourceAllocation>` — scanning every allocation document. On every `AcquireAsync`, two of these scans happen sequentially.

**Add counter-based active counts** using `IDocumentStore`'s existing atomic counter API. Maintain two counter families:

- `alloc-count:pool:{poolId}` — total active count per pool
- `alloc-count:client:{poolId}:{clientId}` — per-client active count per pool

Use a generous TTL on these counters (e.g., 24 hours, refreshed on every write) to avoid premature expiry.

**Update `ResourceAllocationRepository`:**
- `CreateAsync`: After `SetAsync`, increment both counters via `IncrementCounterAsync`.
- `MarkReleasedAsync`: After marking released, decrement both counters (use `GetCounterAsync` + `SetCounterAsync` to subtract 1, floored at 0).
- `CleanupExpiredAsync`: After marking each expired allocation released, decrement the same counters.
- `GetActiveCountAsync`: Read from `GetCounterAsync` instead of scanning.
- `GetActiveCountByClientAsync`: Read from `GetCounterAsync` instead of scanning.

For initial correctness on first startup (or if counters drift), add a `ReconcileCountersAsync` method that does a one-time scan and sets counters to the actual values. Call it from `AllocationCleanupService` on its first cycle.

**Important:** `GetActiveCountAsync` and `GetActiveCountByClientAsync` now return `int` from a counter read. They do NOT return `IReadOnlyList<T>` or construct a `List<T>`. If any calling code uses `.Count()` on a returned collection, update it to use the integer return value directly.

Files to touch:
- `ClientManager.DataAccess/Databases/Implementations/ResourceAllocationRepository.cs`
- `ClientManager.DataAccess/Databases/Interfaces/IResourceAllocationRepository.cs` (add `ReconcileCountersAsync`)
- `ClientManager.Api/Services/Background/AllocationCleanupService.cs` (call reconcile on first cycle)

### 4. Add compound state-store operations for TokenBucket

`TokenBucketStrategy.EvaluateAsync` makes **4 sequential state-store calls**: 2 reads (tokens + lastRefill), then 2 writes. `PeekAsync` makes 2 reads.

**Add batch methods to `IRateLimitStateStore`:**

```csharp
Task<IReadOnlyDictionary<string, long>> GetMultipleCountsAsync(
    IEnumerable<string> keys, CancellationToken ct);

Task SetMultipleCountsAsync(
    IReadOnlyDictionary<string, (long value, TimeSpan window)> entries, CancellationToken ct);
```

**Implement in `RateLimitStateStore`** by delegating to individual `IDocumentStore` counter calls (the batching is at the service boundary — if the store is Redis, future optimization can pipeline them).

**Update `TokenBucketStrategy`:**
- `EvaluateAsync`: Read both keys in one `GetMultipleCountsAsync` call, compute new values, write both in one `SetMultipleCountsAsync` call. 2 round trips instead of 4.
- `PeekAsync`: Read both keys in one `GetMultipleCountsAsync` call. 1 round trip instead of 2.

**Optionally update `ApproximateSlidingWindowStrategy.PeekAsync`** to use `GetMultipleCountsAsync` for its 2 reads (~marginal since it's already 2 calls, but maintains the pattern).

Files to touch:
- `ClientManager.DataAccess/Databases/Interfaces/IRateLimitStateStore.cs`
- `ClientManager.DataAccess/Databases/Implementations/RateLimitStateStore.cs`
- `ClientManager.Api/Services/RateLimiting/TokenBucketStrategy.cs`
- `ClientManager.Api/Services/RateLimiting/ApproximateSlidingWindowStrategy.cs` (optional)

### 5. Introduce `MetricTagKey` enum for metric tag keys

Currently every `TagList` entry in `AccessControlService` and `ResourceAllocationService` uses raw string literals for tag keys (`"clientId"`, `"serviceId"`, `"resourcePoolId"`, `"allocationId"`, `"reason"`). These are repeated dozens of times and are error-prone.

**Create `ClientManager.Api/Services/Instrumentation/MetricTagKey.cs`** with an enum:

```csharp
public enum MetricTagKey
{
    ClientId,
    ServiceId,
    ResourcePoolId,
    AllocationId,
    Reason
}
```

**Add a `ToTagName()` extension** in the same file (small related types in one file is acceptable, following the `DenialReasons.cs` pattern):

```csharp
public static string ToTagName(this MetricTagKey key) => key switch
{
    MetricTagKey.ClientId => "clientId",
    // ...
};
```

**Replace all string literal tag keys** in `AccessControlService`, `ResourceAllocationService`, and `RateLimitService` with `MetricTagKey.X.ToTagName()`. This is a mechanical find-and-replace across the files already being touched in this step.

Files to touch:
- `ClientManager.Api/Services/Instrumentation/MetricTagKey.cs` (new file)
- `ClientManager.Api/Services/AccessControlService.cs`
- `ClientManager.Api/Services/ResourceAllocationService.cs`
- `ClientManager.Api/Services/RateLimiting/RateLimitService.cs`

### 6. Document the performance rationale on changed methods

Every method signature changed in this step must have its XML doc comment updated to explain the performance rationale:

- `RateLimitService.CheckAndIncrementAsync(ClientConfiguration, ...)`: Document that accepting `ClientConfiguration` avoids a redundant `GetByIdAsync` call — the caller already loaded the config.
- `RateLimitService.CheckGlobalServiceLimitAsync(ClientConfiguration, ...)`: Same, plus note that the global rate limit is served from a 30-second `IMemoryCache` to avoid a `GetAll + FirstOrDefault` collection scan.
- `ResourceAllocationRepository.GetActiveCountAsync`: Document that it reads from a maintained atomic counter instead of scanning all allocations.
- `IRateLimitStateStore.GetMultipleCountsAsync` / `SetMultipleCountsAsync`: Document that these exist to batch state-store round trips for strategies like `TokenBucketStrategy` that need multiple keys per evaluation.

## Verification

- The API project compiles without errors.
- Access checks still deny disabled clients, disabled services, missing config, and rate-limited traffic exactly as before — no behavioral change.
- Resource acquisition still enforces per-client and pool-wide caps correctly after acquire, release, and cleanup cycles.
- **UI: Navigate to `/` — verify dashboard stat cards (RPM, pool acquisition %) populate with current data, no error banners.**
- **UI: Navigate to `/monitor` — select different services and clients from the filters, wait for at least one polling refresh, and verify charts and breakdown tables still render correctly.**
- **UI: Navigate to `/allocations` — acquire and release slots via the API HTTP file, then verify pool totals and per-client rows update correctly on the next poll cycle.**
- **UI: Take a screenshot of `/monitor` to confirm no visual regression.**
