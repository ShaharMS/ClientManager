# Plan: Split Public API from Storage Service — Step 5: Caching and Rollout

> **Status**: ✅ Completed
> **Prerequisite**: [api-storage-split-4-read-models-cleanup.md](api-storage-split-4-read-models-cleanup.md)
> **Next**: None — this is the final step.
> **Parent**: [api-storage-split-overview.md](api-storage-split-overview.md)

## TL;DR

Once the split works functionally, harden it for real load: put cache ownership in the right app, add internal-call resiliency, document deployment topology, and validate behavior under traffic. This is the step that turns the split from an architectural diagram into something safe to run at scale, without undoing the simplification by layering in extra duplicate caches or parallel code paths.

## Reference Pattern

In [../../ClientManager.Api/Services/Implementations/RateLimiting/RateLimitService.cs](../../ClientManager.Api/Services/Implementations/RateLimiting/RateLimitService.cs):
- Preserve the idea of short-lived config caching, but move ownership of authoritative cache state closer to the storage-facing app.
- Keep the cache surface narrow and explicit instead of letting every controller or client add its own hidden cache.

In [../../ClientManager.AdminUI/Services/ClientApiService.cs](../../ClientManager.AdminUI/Services/ClientApiService.cs):
- Invalidate local caches on writes immediately.
- Use lightweight client-side caching only for read-mostly UI paths; do not let UI caches become the system of record.

In [../../_scripts/seed_data.py](../../_scripts/seed_data.py):
- Preserve the existing external startup and seeding workflow as much as possible.
- Update scripts and documentation only where the new two-app topology requires explicit coordination.

## Steps

### 1. Define cache ownership and invalidation rules

Move the existing short-lived cache behavior to `ClientManager.StorageApi` for read-mostly configuration and statistics queries where it helps, and use explicit invalidation on writes. Prefer re-homing the current cache logic over introducing a new caching subsystem.

`ClientManager.Api` should usually add no new cache at all. If a tiny non-authoritative cache is retained for repeated reads, keep it narrow and measurable, and always treat `ClientManager.StorageApi` as the source of truth.

```csharp
public sealed class StorageReadCacheOptions
{
    public TimeSpan CatalogTtl { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan StatisticsTtl { get; init; } = TimeSpan.FromSeconds(5);
}
```

Keep cache option types local unless the same type is truly referenced across multiple projects.

### 2. Add internal-call resilience and deployment documentation

Configure outbound `HttpClient` timeouts, retry behavior for idempotent reads, and circuit-breaking or fast-fail behavior for the public API when `ClientManager.StorageApi` is unavailable.

Document the deployment topology in repo docs and settings files: public `ClientManager.Api` instances talk only to internal `ClientManager.StorageApi`; only the storage-facing app references `ClientManager.DataAccess`; multi-instance production must use Redis or MongoDB instead of shared JSON files. Keep the resilience setup simple and built on the existing `HttpClient` stack rather than adding a large custom abstraction.

### 3. Run load-focused validation and update local run instructions

Update startup instructions so local and production environments clearly show the new order: `ClientManager.StorageApi`, then `ClientManager.Api`, then `ClientManager.AdminUI`, plus the existing seed and traffic-generator tools.

Use the existing traffic generator and dashboard flows to validate that the added internal hop does not erase the gains from centralized storage access and cache ownership.

## Verification

- Configuration and statistics cache rules are documented and implemented in the storage-facing app, with write operations invalidating affected entries.
- The final design has one authoritative implementation per behavior and one canonical shared definition per cross-project contract; no duplicate cache or transport DTO layers were introduced during rollout.
- The public API fails predictably when `ClientManager.StorageApi` is down, rather than hanging or surfacing low-level transport exceptions.
- UI: Navigate to `/settings` or any configuration page, make a change, and verify the updated value appears consistently when revisiting `/clients`, `/services`, or `/resource-pools`.
- UI: Run the traffic generator, open `/` and `/monitor`, and verify charts continue updating without obvious lag spikes or repeated error notifications.
- UI: Open `/allocations` after sustained traffic and verify active allocation counts remain coherent, confirming the split did not introduce stale or duplicated state under load.
