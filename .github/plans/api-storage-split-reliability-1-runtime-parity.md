# Plan: Harden API/Storage Split Reliability — Step 1: Runtime Parity

> **Status**: ✅ Completed
> **Prerequisite**: None — this is the first step.
> **Next**: [api-storage-split-reliability-2-transport-contracts.md](api-storage-split-reliability-2-transport-contracts.md)
> **Parent**: [api-storage-split-reliability-overview.md](api-storage-split-reliability-overview.md)

## TL;DR

Audit the moved runtime services against the pre-split behavior and fix every semantic regression before tuning anything else. This step is about proving that access checks, allocation acquire/release, usage recording, and rate-limit algorithms still mean the same thing after the split instead of merely compiling in a new project.

## Reference Pattern

In [../../ClientManager.Api/Services/Implementations/AccessControlService.cs](../../ClientManager.Api/Services/Implementations/AccessControlService.cs):
- Preserve the original deny-by-default flow, exception semantics, and usage side effects.
- Match the prior order of checks so rate limits and configuration denials do not drift.

In [../../ClientManager.Api/Services/Implementations/ResourceAllocationService.cs](../../ClientManager.Api/Services/Implementations/ResourceAllocationService.cs):
- Preserve acquire and release semantics, especially client-cap checks, pool-cap checks, and release idempotency.
- Keep metrics and usage recording aligned with allocation outcomes.

In [../../ClientManager.Api/Services/Implementations/RateLimiting/RateLimitService.cs](../../ClientManager.Api/Services/Implementations/RateLimiting/RateLimitService.cs):
- Treat the original implementation as the semantic baseline for client-level and global limit evaluation.
- Verify each moved strategy against the original allowed/denied thresholds and retry-after behavior.

In [../../ClientManager.Api/Services/Implementations/UsageTracking/UsagePersistenceService.cs](../../ClientManager.Api/Services/Implementations/UsageTracking/UsagePersistenceService.cs):
- Preserve the two-loop flush and rollup behavior and avoid subtle bucket drift.
- Keep runtime-side persistence close to storage, but do not weaken the previous data retention behavior.

## Steps

### 1. Diff every moved runtime service against the original public-host implementation

Compare the new storage-host implementations to the deleted `ClientManager.Api` versions line by line for:

- access-check ordering and thrown exception types
- resource acquisition denial reasons and release semantics
- rate-limit allowed/denied thresholds, retry-after calculations, and global-limit side effects
- usage recording and metrics side effects

Do not accept refactors that change meaning without an explicit reason.

```csharp
Task<RateLimitResult> CheckAndIncrementAsync(
    ClientConfiguration config,
    string serviceId,
    CancellationToken cancellationToken = default);
```

### 2. Fix semantic drift in the storage-host runtime layer

Update the storage-host runtime services and controllers where behavior no longer matches the old system. Pay special attention to off-by-one limit decisions, changed release return shapes, changed denial classifications, and any controller or middleware behavior that makes the public API surface differ from the old contract.

### 3. Verify the moved background jobs still preserve runtime invariants

Inspect `AllocationCleanupService`, `UsagePersistenceService`, and `DataSeedService` in `ClientManager.StorageApi` to ensure startup, cleanup, rollup, and retention behavior still reflect the original single-host expectations. If a hosted service now relies on transport-facing behavior or changed option binding, correct that before moving to client transport fixes.

## Verification

- `ClientManager.StorageApi` compiles with the moved runtime services and no semantic TODO placeholders remain.
- Access checks and resource allocation decisions match the pre-split behavior for happy path, disabled client/service, not-configured access, rate-limited, and no-slot cases.
- Usage flush, rollup, and cleanup jobs still mutate snapshots and allocations as expected under local traffic.
- UI: Navigate to `/monitor` and verify live traffic still updates access and allocation activity without new empty states or repeated errors.
- UI: Navigate to `/allocations`, acquire a slot, release it, then repeat the release and verify the workflow stays coherent and does not duplicate or lose state.
- UI: Open `/` while traffic is running and verify overview counters continue moving instead of freezing after the runtime split.