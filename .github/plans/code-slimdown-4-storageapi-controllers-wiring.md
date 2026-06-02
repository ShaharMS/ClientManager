# Plan: Code Slim-Down — Step 4: Delete the Transport Layer and the StorageApi Host

> **Status**: 🔲 Not started
> **Prerequisite**: [code-slimdown-3-storageapi-services.md](code-slimdown-3-storageapi-services.md)
> **Next**: [code-slimdown-5-api-services.md](code-slimdown-5-api-services.md)
> **Parent**: [code-slimdown-overview.md](code-slimdown-overview.md)

## TL;DR

Second half of removing the StorageApi: cut the API's controllers/services over to the in-process services from step 3, then delete the entire transport layer — the `Services/Internal` storage clients + interfaces, `StorageApiRoutes`, `StorageApiResilienceHandler`, `HotPathFailOpenFilter`, the HTTP client registration — and delete the `ClientManager.StorageApi` project itself. Update the solution, docker-compose, scripts, and the local-testing runbook. This is the single largest LOC reduction in the plan.

## Iteration Bootstrap

- **Iteration slug**: `code-slimdown`
- **Required evidence**: `dotnet build ClientManager.slnx` clean after StorageApi is removed from the solution; `ClientManager.DataAccess.Tests` pass; full stack runs with **only** Api + AdminUI (no StorageApi); public API endpoints return identical responses; `git diff --stat` shows a large net deletion.
- **UI artifacts to verify**: Every AdminUI page works against the API-only backend with live traffic.
- **Commit-splitting guidance**: (a) cut controllers/services over to in-process services, (b) delete internal clients + transport contracts + resilience + fail-open filter, (c) remove StorageApi project from solution + delete the project, (d) update docker-compose/scripts/runbook docs.

## Reference Pattern

In [ClientManager.Api/Services/Internal/](ClientManager.Api/Services/Internal/) (`RuntimeStateClient`, `StatisticsReadClient`, `ServiceCatalogClient`, `ResourcePoolCatalogClient`, `GlobalRateLimitCatalogClient`, `ClientConfigurationStoreClient` + interfaces):
- These are the HTTP transport bindings to delete. Every consumer of an `IRuntimeStateClient`/`IStatisticsReadClient`/`I*CatalogClient` must be repointed to the matching in-process service relocated in step 3.

In [ClientManager.Api/Utils/StorageApi/StorageApiResilienceHandler.cs](ClientManager.Api/Utils/StorageApi/StorageApiResilienceHandler.cs), [ClientManager.Api/Filters/HotPathFailOpenFilter.cs](ClientManager.Api/Filters/HotPathFailOpenFilter.cs), and [ClientManager.Shared/Contracts/Storage/StorageApiRoutes.cs](ClientManager.Shared/Contracts/Storage/StorageApiRoutes.cs):
- All exist solely to survive a remote storage hop that no longer exists — delete them. (Note: this supersedes the route template-ization from step 1; `StorageApiRoutes` is removed, not refactored.)

## Steps

### 1. Repoint API services/controllers to the in-process services

Replace every injection of an `I*Client` transport interface with the corresponding relocated in-process service from step 3. The API's existing pass-through services (`AccessControlService`, `ResourceAllocationService`, `MetricsService`, and the API-side catalog adapters) either become thin shims over the in-process services or are deleted in favor of injecting the in-process service directly into controllers. Keep controllers thin and their `[ProducesResponseType]` codes unchanged. Use find-all-references to ensure no transport interface remains referenced.

### 2. Delete the internal transport layer

Delete the entire [ClientManager.Api/Services/Internal/](ClientManager.Api/Services/Internal/) folder (clients + interfaces), [StorageApiResilienceHandler.cs](ClientManager.Api/Utils/StorageApi/StorageApiResilienceHandler.cs) and its `Utils/StorageApi` folder, [HotPathFailOpenFilter.cs](ClientManager.Api/Filters/HotPathFailOpenFilter.cs), and [StorageApiRoutes.cs](ClientManager.Shared/Contracts/Storage/StorageApiRoutes.cs). Remove the typed `HttpClient`/resilience registrations and the fail-open filter registration from [ClientManager.Api/Program.cs](ClientManager.Api/Program.cs). Remove the `[HotPathFailOpen]` attribute usages on controllers. Delete any now-orphaned transport DTOs in `Shared/Contracts/Storage` that the public API does not use.

### 3. Delete the StorageApi project

Remove `ClientManager.StorageApi` from [ClientManager.slnx](ClientManager.slnx) and delete the project directory. Verify nothing else references it (the AdminUI and API should not). Confirm `dotnet build ClientManager.slnx` succeeds.

### 4. Update deployment + tooling + docs

Update [docker-compose.yml](docker-compose.yml) and [docker-compose.dev.yml](docker-compose.dev.yml) to drop the StorageApi service and any `StorageApi__BaseUrl`-style env wiring. Update `_scripts` and the `.github/copilot-instructions.md` "Local Testing" runbook so the sequence is **Api → AdminUI → seed → traffic** (no StorageApi host, no StorageApi port). Update any README references to the two-host architecture.

### 5. Confirm fail-open semantics are acceptable

With no remote storage dependency, hot-path calls now hit in-process storage directly; the previous "fail open when StorageApi errors" behavior no longer applies. Confirm this is acceptable (in-process storage errors should surface as real errors via the global exception filter, not be silently granted). Note this behavior change explicitly in the commit message.

## Verification

- `dotnet build ClientManager.slnx` compiles with StorageApi removed.
- `ClientManager.DataAccess.Tests` pass.
- Grep confirms zero references to `IRuntimeStateClient`, `IStatisticsReadClient`, `StorageApiRoutes`, `StorageApiResilienceHandler`, `HotPathFailOpen`.
- Public API parity: access-check, acquire/release, metrics, statistics, and catalog CRUD all return the same shapes/status codes as before, now served in-process.
- `git diff --stat` shows a large net deletion (entire project + transport layer).
- **UI: Start ONLY Api + AdminUI (+ seed + traffic). Load every page — `/`, `/clients`, `/services`, `/resourcepools`, `/ratelimits`, `/quotas`, Monitor, Allocations — and confirm live data with no errors.**
- **UI: Exercise CRUD (create/edit/delete a Service) and confirm persistence against in-process storage.**
- **UI: Screenshot the Dashboard under live traffic to confirm parity with the pre-merge system.**
