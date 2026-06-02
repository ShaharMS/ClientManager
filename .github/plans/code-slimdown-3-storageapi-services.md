# Plan: Code Slim-Down — Step 3: Relocate Storage Services into the API

> **Status**: 🔲 Not started
> **Prerequisite**: [code-slimdown-2b-storage-bindings.md](code-slimdown-2b-storage-bindings.md)
> **Next**: [code-slimdown-4-storageapi-controllers-wiring.md](code-slimdown-4-storageapi-controllers-wiring.md)
> **Parent**: [code-slimdown-overview.md](code-slimdown-overview.md)

## TL;DR

First half of removing the StorageApi host: move its real domain logic — `AccessControlService`, `ResourceAllocationService`, `RateLimitService`, `StatisticsService`, the four catalog services, `ClientConfigurationCatalogService`, seeding, and the background services — into the API project so they run **in-process against `ClientManager.DataAccess`**. Register them in the API's DI. This step adds the in-process path **alongside** the existing transport path so the API still compiles and runs; the old transport layer is deleted in step 4. Behavior is preserved; this is a relocation, not a rewrite.

## Iteration Bootstrap

- **Iteration slug**: `code-slimdown`
- **Required evidence**: `dotnet build ClientManager.Api` clean with the relocated services registered; `ClientManager.DataAccess.Tests` still pass; the API can resolve the in-process services from DI at startup; `git diff --stat` (net may be ~flat this step — lines move, not deleted; deletions land in step 4).
- **UI artifacts to verify**: Full stack still runs and AdminUI pages still load (the API may still be using the transport path at this point — that's fine; the in-process services are wired but cut-over happens in step 4).
- **Commit-splitting guidance**: One commit per service family moved — (a) catalog/config services, (b) access/resource/rate-limit runtime services, (c) statistics, (d) seeding + background services + DI registration.

## Reference Pattern

In [ClientManager.StorageApi/Services/Implementations/](ClientManager.StorageApi/Services/Implementations/):
- These services already call `DataAccess` databases/repositories directly — this is exactly the in-process shape the API needs. They are the source of truth to relocate.

In [ClientManager.StorageApi/Program.cs](ClientManager.StorageApi/Program.cs) and [ClientManager.StorageApi/Utils/Extensions/ServiceCollectionExtensions.cs](ClientManager.StorageApi/Utils/Extensions/ServiceCollectionExtensions.cs):
- The DI registrations for these services + the storage providers + background services are the wiring to reproduce in the API.

In [ClientManager.Api/Program.cs](ClientManager.Api/Program.cs):
- The API already registers `DataAccess` storage providers for nothing today (it goes over HTTP) — confirm and reuse the same `AddStorageProviders`/repository registration the StorageApi uses.

## Steps

### 1. Decide the home for the relocated services

Move the StorageApi service implementations + their interfaces into `ClientManager.Api` (e.g., under `ClientManager.Api/Services/Storage/` or a new `ClientManager.Core` class library if you prefer a cleaner project boundary). Keep namespaces sensible and update them consistently. The simplest lowest-line option is to place them directly in the API; only introduce a new project if it genuinely aids organization.

### 2. Relocate the catalog and configuration services

Move `ServiceCatalogService`, `ResourcePoolCatalogService`, `GlobalRateLimitCatalogService`, and `ClientConfigurationCatalogService` (plus `ClientLookup` if retained) and their interfaces into the API. They already depend only on `DataAccess` repositories/databases + caching. Fix usings; do not change their logic yet (generic consolidation is step 5).

### 3. Relocate the runtime services

Move `AccessControlService`, `ResourceAllocationService`, and `RateLimitService` (+ the rate-limit strategy classes) into the API. These carry the telemetry scaffolding that step 5 will slim — leave it intact here. Preserve activity/metric names exactly.

### 4. Relocate statistics, seeding, and background services

Move `StatisticsService`, `DataSeedService`, `AllocationCleanupService`, and `UsagePersistenceService` (and `UsageRecorder`) into the API. These become the API's own hosted services running against in-process storage.

### 5. Register everything in the API DI

In [ClientManager.Api/Program.cs](ClientManager.Api/Program.cs) (or a new API `ServiceCollectionExtensions`), register the storage providers (`AddStorageProviders`/repositories) and all relocated services + hosted services, mirroring the StorageApi composition root. At this point both the in-process services and the old HTTP transport exist; that is expected and temporary.

## Verification

- `dotnet build ClientManager.Api` compiles with the relocated services and DI registrations.
- `ClientManager.DataAccess.Tests` pass.
- API starts and the DI container resolves the relocated services + hosted services without exceptions (check startup logs).
- `git diff --stat` (expect roughly net-neutral or slight growth this step; the deletions happen in step 4).
- **UI: Start the full stack (StorageApi may still be running and in use) and confirm AdminUI pages load — this step must not break the running system.**
- **UI: Screenshot the Dashboard to confirm no regression.**
