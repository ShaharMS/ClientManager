# Plan: Statistics and Enforcement Performance Optimization — Step 2: Enforcement Hot Path

> **Status**: 🔲 Not started
> **Prerequisite**: [statistics-and-enforcement-optimization-1-baselines.md](statistics-and-enforcement-optimization-1-baselines.md)
> **Next**: [statistics-and-enforcement-optimization-3-usage-storage.md](statistics-and-enforcement-optimization-3-usage-storage.md)
> **Parent**: [statistics-and-enforcement-optimization-overview.md](statistics-and-enforcement-optimization-overview.md)

## TL;DR

Make immediate blocking checks cheaper by eliminating duplicate repository fetches, replacing scan-based counters, and reducing round trips inside rate-limit strategy evaluation. This step is the highest-priority guard against longer retention impacting the actual purpose of the app.

## Reference Pattern

[../../ClientManager.Api/Services/AccessControlService.cs](../../ClientManager.Api/Services/AccessControlService.cs) shows the current early-return flow for access checks and where repeated lookups are happening.

In [../../ClientManager.Api/Services/AccessControlService.cs](../../ClientManager.Api/Services/AccessControlService.cs):
- Keep the deny-by-default ordering and exception behavior unchanged.
- Reuse already-resolved entities instead of re-fetching the same configuration inside downstream services.

[../../ClientManager.Api/Services/UsageTracking/UsageBuffer.cs](../../ClientManager.Api/Services/UsageTracking/UsageBuffer.cs) demonstrates the intended hot-path pattern: a simple in-memory structure with cheap updates and deferred background processing.

In [../../ClientManager.Api/Services/UsageTracking/UsageBuffer.cs](../../ClientManager.Api/Services/UsageTracking/UsageBuffer.cs):
- Favor lock-light, keyed, singleton state for hot reads and writes.
- Keep mutation localized and cheap on the request path.

## Steps

### 1. Reuse a single evaluation context per request

Refactor `ClientManager.Api/Services/AccessControlService.cs`, `ClientManager.Api/Services/ResourceAllocationService.cs`, and `ClientManager.Api/Services/RateLimiting/RateLimitService.cs` so the outer service resolves configuration once and passes a small immutable evaluation context into rate-limit evaluation instead of forcing `RateLimitService` to load the same client configuration again.

```csharp
public sealed record AccessEvaluationContext(
    ClientConfiguration Client,
    ServiceAccessSettings? ServiceSettings,
    GlobalRateLimit? GlobalServiceLimit);
```

Do the same for resource-pool checks so `AcquireAsync` does not fetch client state and then ask `RateLimitService` to fetch it again.

### 2. Replace scan-based global-limit lookup with keyed access

`ClientManager.DataAccess/Databases/Implementations/GlobalRateLimitRepository.cs` currently loads all global limits and filters in memory. Introduce deterministic IDs or an internal target index so `GetByTargetAsync` becomes a direct lookup.

Touch at least:

- `ClientManager.Shared/Models/Entities/GlobalRateLimit.cs` if the ID shape must be standardized.
- `ClientManager.DataAccess/Databases/Implementations/GlobalRateLimitRepository.cs`
- Any create/update paths that persist global limits.

Keep backward compatibility in mind if existing seed data uses older IDs.

### 3. Add maintained allocation counters for quota checks

`ClientManager.DataAccess/Databases/Implementations/ResourceAllocationRepository.cs` currently scans all allocations for `GetActiveCountAsync` and `GetActiveCountByClientAsync`. Add explicit active-count state keyed by pool and by `(pool, client)`, and update it on create, release, and cleanup.

Possible shape:

```csharp
public sealed record AllocationCountKey(string PoolId, string? ClientId);
```

Use that state from `ClientManager.Api/Services/ResourceAllocationService.cs` so the quota and capacity checks are constant-time reads instead of full collection scans.

### 4. Reduce rate-limit state-store round trips where strategies need multiple reads

`TokenBucketStrategy` currently performs multiple sequential store calls. Extend `ClientManager.DataAccess/Databases/Interfaces/IRateLimitStateStore.cs` with a small compound state API or batched read/write operations, then update:

- `ClientManager.Api/Services/RateLimiting/TokenBucketStrategy.cs`
- `ClientManager.Api/Services/RateLimiting/ApproximateSlidingWindowStrategy.cs` where batching is also useful
- The state-store implementations in `ClientManager.DataAccess`

The goal is fewer backend round trips while preserving correctness and expiry semantics.

## Verification

- The API project builds without errors.
- Baseline benchmarks from Step 1 show lower P95 latency for access checks and resource acquisitions with behavior unchanged.
- Access checks still deny disabled clients, disabled services, missing config, and rate-limited traffic exactly as before.
- Resource acquisition still enforces per-client and pool-wide caps correctly after acquire, release, and cleanup cycles.
- UI: Navigate to `/monitor`, let the page refresh at least once, and verify request charts and breakdown tables continue to populate without error alerts.
- UI: Navigate to `/allocations`, acquire and release some pool slots through the API or HTTP file, then verify pool totals and client allocation rows reflect the updated counts.
- UI: Navigate to `/` and verify dashboard stat cards still show current totals with no missing data or stale-error banners.