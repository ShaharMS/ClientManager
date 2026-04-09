# Plan: Split Public API from Storage Service — Step 3: Runtime State

> **Status**: ✅ Completed
> **Prerequisite**: [api-storage-split-2-configuration-split.md](api-storage-split-2-configuration-split.md)
> **Next**: [api-storage-split-4-read-models-cleanup.md](api-storage-split-4-read-models-cleanup.md)
> **Parent**: [api-storage-split-overview.md](api-storage-split-overview.md)

## TL;DR

Move concurrency-sensitive runtime behavior into `ClientManager.StorageApi` so rate limiting, allocation decisions, access checks, usage persistence, and cleanup execute in the same app boundary that owns storage. This is the core split that removes race-prone storage orchestration from the public API hot path, and it should be implemented mainly by relocating the current runtime services rather than re-creating them in both hosts.

## Reference Pattern

In [../../ClientManager.Api/Services/Implementations/AccessControlService.cs](../../ClientManager.Api/Services/Implementations/AccessControlService.cs):
- Preserve the existing access-check business rules and exception semantics.
- Keep the operation coarse-grained: one command should perform all storage-backed checks needed for a decision.

In [../../ClientManager.Api/Services/Implementations/ResourceAllocationService.cs](../../ClientManager.Api/Services/Implementations/ResourceAllocationService.cs):
- Preserve the current acquire/release flow, including client caps, global limit checks, allocation creation, and metric/usage side effects.
- Keep allocation decisions near the counter and allocation state so the split does not introduce new race windows.

In [../../ClientManager.Api/Services/Implementations/RateLimiting/RateLimitService.cs](../../ClientManager.Api/Services/Implementations/RateLimiting/RateLimitService.cs):
- Treat rate-limit evaluation as a storage-adjacent concern because it depends on atomic counter operations.
- Keep short-lived caching close to the storage-facing app rather than scattering config caches across public API nodes.

In [../../ClientManager.Api/Services/Implementations/AllocationCleanupService.cs](../../ClientManager.Api/Services/Implementations/AllocationCleanupService.cs):
- Hosted jobs that reconcile counters or flush state belong in the storage-facing app, not the public API.
- Preserve the first-cycle reconciliation behavior so counter drift handling does not regress.

## Steps

### 1. Move runtime services and rate-limit strategies into the storage-facing app

Port the current access-control, resource-allocation, rate-limit, and usage-tracking service implementations into `ClientManager.StorageApi`, keeping `ClientManager.DataAccess` references only there.

Do not expose repository-shaped remote methods. Expose runtime operations at the same granularity as the current public service methods so each request stays a single internal hop. Reuse existing shared runtime request and response types such as `CheckAccessRequest`, `AcquireResourceRequest`, `ReleaseResourceRequest`, `AccessCheckResponse`, `ResourceAcquireResponse`, and `ClientAccessibilityResponse` wherever they already fit.

```csharp
public interface IRuntimeStateService
{
    Task<AccessCheckResponse> CheckAccessAsync(CheckAccessRequest request, CancellationToken cancellationToken);
    Task<ResourceAcquireResponse> AcquireAsync(AcquireResourceRequest request, CancellationToken cancellationToken);
}
```

Only add a new shared internal contract if the current shared request and response models cannot represent an operation cleanly.

### 2. Move hosted storage jobs into `ClientManager.StorageApi`

Register `AllocationCleanupService`, `UsagePersistenceService`, and `DataSeedService` only in `ClientManager.StorageApi`. Their storage access is part of the same ownership boundary as counters, snapshots, and allocations.

This step is where the public API stops owning background storage mutation entirely.

### 3. Replace public API runtime implementations with internal RPC adapters

Refactor the public `IAccessControlService` and `IResourceAllocationService` implementations so they call the new internal runtime endpoints instead of using `ClientManager.DataAccess` directly.

Keep the public controllers unchanged where possible. The goal is to preserve the external surface while swapping the implementation behind it. These adapters should stay intentionally small so the business logic lives in one place, inside `ClientManager.StorageApi`.

## Verification

- Access checks and resource allocation flows still succeed through the public API after the runtime services are remote-backed.
- Runtime endpoints share one canonical contract definition in `ClientManager.Shared`; there are no duplicate request or response types under either host project.
- Background jobs that flush usage and clean up expired allocations run only in `ClientManager.StorageApi`.
- UI: Navigate to `/monitor` and verify live access/resource activity still updates without repeated request failures.
- UI: Navigate to `/allocations`, acquire and release resources through the normal workflow, and verify the list updates without stale counts or duplicate allocations.
- UI: Open `/` after traffic is running and verify dashboard tiles continue to change over time rather than freezing after the runtime split.
