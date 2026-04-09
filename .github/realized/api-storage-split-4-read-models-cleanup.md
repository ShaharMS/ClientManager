# Plan: Split Public API from Storage Service — Step 4: Read Models and Cleanup

> **Status**: ✅ Completed
> **Prerequisite**: [api-storage-split-3-runtime-state.md](api-storage-split-3-runtime-state.md)
> **Next**: [api-storage-split-5-caching-rollout.md](api-storage-split-5-caching-rollout.md)
> **Parent**: [api-storage-split-overview.md](api-storage-split-overview.md)

## TL;DR

Move statistics, exporters, and other read-model composition behind `ClientManager.StorageApi`, then remove the final `ClientManager.DataAccess` dependency from the public API project. This is the step that makes the architectural split complete rather than partial, and it should continue the same move-first pattern instead of creating a second read-model layer in the public host.

## Reference Pattern

In [../../ClientManager.Api/Controllers/StatisticsController.cs](../../ClientManager.Api/Controllers/StatisticsController.cs):
- Preserve the public route surface and response shapes, but eliminate direct storage dependencies from the controller constructor.
- Collapse multi-repository read logic behind a single service boundary instead of rebuilding it in the public API.

In [../../ClientManager.Api/Services/Implementations/UsageTracking/UsagePersistenceService.cs](../../ClientManager.Api/Services/Implementations/UsageTracking/UsagePersistenceService.cs):
- Keep usage rollup and pruning logic next to the snapshot store that it mutates.
- Preserve the two-loop fast/slow processing model when moving the responsibility to the storage-facing app.

In [../../ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs](../../ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs):
- Use this as the checklist for everything that must leave the public API’s DI graph.
- Remove storage registrations only after equivalent internal clients and façade services are in place.

## Steps

### 1. Move statistics and exporter composition into the storage-facing app

Port `StatisticsService`, Prometheus export composition, Grafana export composition, and any remaining storage-bound read services to `ClientManager.StorageApi`.

Expose internal query endpoints for dashboard overview, client/service/pool statistics, and exporter payloads so the public API no longer builds those read models itself. Reuse existing shared statistics and metrics response models first; only create new shared internal query or response types if an existing shared model is not enough.

```csharp
public interface IStatisticsReadClient
{
    Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken);
    Task<SearchResult<ClientSummaryResponse>> SearchClientsAsync(DocumentQuery query, CancellationToken cancellationToken);
}
```

### 2. Refactor public read controllers and metrics endpoints to use internal clients

Update `StatisticsController`, metrics/export endpoints, and any remaining public services so they proxy to `ClientManager.StorageApi` instead of referencing data-access abstractions.

At the end of this step, every public API controller should depend only on local façade services or internal HTTP clients. Remove duplicate read composition from `ClientManager.Api` as soon as the storage-facing version is active.

### 3. Remove the data-access dependency from the public API project

Delete the `ClientManager.DataAccess` project reference from `ClientManager.Api.csproj`, remove leftover `using ClientManager.DataAccess...` statements, and strip storage registration code from `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs`.

If a class in `ClientManager.Api` still needs a data-access abstraction after this pass, that is a boundary violation and should be migrated before the project reference is removed.

## Verification

- `ClientManager.Api` builds and runs without a `ClientManager.DataAccess` project reference.
- Statistics and exporter contracts are sourced from `ClientManager.Shared`, with no duplicate read-model DTOs added under `ClientManager.Api` or `ClientManager.StorageApi`.
- Statistics, Prometheus, and Grafana-style read endpoints still produce the same external payloads after proxying through `ClientManager.StorageApi`.
- UI: Navigate to `/` and verify overview tiles, charts, and client/service breakdowns still load with live data.
- UI: Navigate to `/monitor` and verify charts continue rendering with the expected time-series updates and no blank panels.
- UI: Navigate to `/clients` and `/services` after the cleanup pass and verify general browsing still works, confirming no stray data-access dependency remains in the public API.
