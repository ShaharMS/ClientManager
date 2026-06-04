# Implementation Handoff — code-slimdown

## Current Pass

Step 7: API exceptions and instrumentation ([code-slimdown-7-api-exceptions-instrumentation.md](../../plans/code-slimdown-7-api-exceptions-instrumentation.md)). Prerequisite: Step 6 complete.

## Pass History

| Pass | Date | Summary |
| --- | --- | --- |
| 1 | 2026-06-02 | Step 1 implemented and verified (build + UI) |
| 2 | 2026-06-02 | Step 2 (Data Access Layer) implemented and verified (build + tests + CRUD + UI) |
| 3 | 2026-06-02 | Step 2b (Storage Bindings) implemented and verified (build + tests + JsonFile & Lucene CRUD round-trip + UI edit/persist) |
| 4 | 2026-06-04 | Step 3 (Relocate Storage Services) implemented and verified (build + tests + API DI/hosted-services startup + UI Dashboard) |
| 5 | 2026-06-04 | Step 4 (Delete Transport Layer + StorageApi Host) implemented and verified (build + tests + grep-clean + live traffic + UI CRUD round-trip) |
| 6 | 2026-06-04 | Step 5 (Merged API Services) implemented and verified (API build + DataAccess.Tests; net −220 on storage service refactor) |
| 7 | 2026-06-04 | Step 6 (Merged API Controllers) implemented and verified (API build + live HTTP smoke after restart) |

## Changed Files

- `ClientManager.Shared/Models/Problems/ProblemResponse.cs` — class → record.
- `ClientManager.Shared/Models/Problems/StorageProblemResponse.cs` — class → record (Step 1); **deleted** in Step 4 (in-process problems use `StorageApiProblemException` + `StorageErrorCodes`).
- `ClientManager.AdminUI/Models/UserPreferences.cs` — class → record (kept `set` accessors; Settings.razor mutates).
- `ClientManager.Shared/Logging/IAppLogger.cs` — one signature per level `(message, extraData, exception)`.
- `ClientManager.Shared/Logging/AppLogger.cs` — expression-bodied members for reduced interface.
- Logger call-site fixes (exception binding): RuntimeStateClient, Api/StorageApi ErrorHandlingMiddleware, InstrumentedDocumentStore, UsagePersistenceService, RateLimitService, AllocationCleanupService, AccessControlService, ResourceAllocationService, HotPathFailOpenFilter.
- Enum consolidation: created `Models/Enums/StorageEnums.cs`, `RateLimitEnums.cs`, `UsageEnums.cs`, `Models/Search/SearchEnums.cs`; deleted 8 single-enum files. Namespaces unchanged.
- `ClientManager.Shared/Contracts/Storage/StorageApiRoutes.cs` — added private `Escape` helper; routes identical.

### Step 2 (Data Access Layer)

- `ClientManager.DataAccess/Databases/Implementations/RateLimitStateDatabase.cs` — primary constructor; all 6 methods expression-bodied delegates to `IDocumentStore`. Kept as DI seam interface.
- `ClientManager.DataAccess/Repositories/Implementations/EntityRepository.cs` — primary constructor `(IDocumentStore store, string collection, Func<T,string> idSelector)`; all members expression-bodied. Kept (genuine polymorphic seam: `Service`, `ResourcePool`, base of `IGlobalRateLimitDatabase`).
- `ClientManager.DataAccess/Databases/Implementations/GlobalRateLimitDatabase.cs` — inheritance → composition over a private inner `EntityRepository<GlobalRateLimit>`; explicit one-line `IEntityRepository` delegations; extracted `BuildTargetQuery(targetType, targetId?)` shared by `GetByTargetAsync`/`GetByTargetTypeAsync`.
- `ClientManager.DataAccess/Databases/Implementations/ClientConfigurationDatabase.cs` — primary constructor; added generic `MutateAsync<TValue>` helper folding the four mirrored set/remove service- and resource-pool-settings blocks; simplified `GetResourcePoolSettingsAsync`.
- `ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs` — added `BuildQuery(targetId?, targetType?, granularity?)` + `ExecuteQueryAsync` helpers; `GetByTargetAsync`/`GetAllByGranularityAsync` reduced to build+search. Constructor left as-is to avoid disturbing an unrelated mis-indented region.
- `ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs` — added `ForEachAllocationKey` helper to dedupe pool/client counter-key construction in cleanup + reconcile loops.

### Step 2b (Storage Bindings)

- `ClientManager.DataAccess/Stores/Implementations/Helpers/StoreSerialization.cs` (NEW) — static class exposing shared `JsonSerializerOptions JsonOptions { get; } = new() { PropertyNameCaseInsensitive = true }`.
- `ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs` — removed private `JsonOptions` field; `using static …StoreSerialization`.
- `ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs` — removed private `JsonOptions` field; `using static …StoreSerialization`.
- `ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs` — removed private `JsonOptions` field + `_database` field/old constructor; converted to primary constructor `MongoDBDocumentStore(IMongoDatabase database)`; `<param>` doc moved to class summary.
- `ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs` — removed private `JsonOptions` field; `using static …StoreSerialization`.
- `ClientManager.StorageApi/Utils/Extensions/DocumentStoreFactory.cs` — added generic `GetOrCreate<TKey,TStore>` cache helper + `LoadCertificate`; extracted Mongo (`CreateMongoClient`/`ApplyMongoTls`/`ApplyMongoAuthentication`) and Redis (`CreateRedisMultiplexer`/`BuildRedisConfiguration`) builders; the four `Create*Store` methods now route through `GetOrCreate`.
- `ClientManager.Shared/Configuration/Storage/` — `MongoDbStoreOptions`, `RedisStoreOptions`, `JsonFileStoreOptions`, `LuceneStoreOptions`, `StorageRoleBinding`, `PersistenceOptions` all `class` → `record` (kept `{ get; set; }` properties + XML docs; config binding unchanged).

### Step 3 (Relocate Storage Services)

- `ClientManager.Api/ClientManager.Api.csproj` — added `<ProjectReference Include="..\ClientManager.DataAccess\ClientManager.DataAccess.csproj" />` (API previously referenced only Shared; storage NuGet packages flow transitively via DataAccess).
- `ClientManager.Api/Services/Storage/**` (NEW, 46 .cs files) — copied from `ClientManager.StorageApi` and namespace-rewritten: `Services/**` (Implementations incl. Exporters/RateLimiting/Strategies/UsageTracking, Interfaces, ClientLookup), `Models/{Configuration,Entities,Enums,Exceptions}/**`, `Utils/Instrumentation/**`, and six `Utils/Extensions` files (DocumentStoreFactory, StorageProvider/Repository registration, Denial/MetricTagKey/Pagination extensions). Three ordered namespace replacements applied: `ClientManager.StorageApi.Services→…Api.Services.Storage`, `…StorageApi.Models→…Api.Services.Storage.Models`, `…StorageApi.Utils→…Api.Services.Storage.Utils`. Metric/activity name string literals in `StorageApiMetrics.cs` preserved exactly (still `"ClientManager.StorageApi"`).
- `ClientManager.Api/Services/Storage/StorageServicesRegistration.cs` (NEW) — `AddInProcessStorageServices(services, configuration, environment)` mirrors the Storage API host composition root (providers, repositories, memory cache, validated `StorageReadCacheOptions`, `IStorageReadCache`, rate-limit strategies/resolver, runtime services, read-model services, four catalog services, background hosted services, conditional `DataSeedService`, `UsageTrackingOptions`) plus the `StorageApiMetrics` singleton. Intentionally does NOT re-register `IAppLogger<>` (API already adds it via `AddPublicApiServices`).
- `ClientManager.Api/Program.cs` — added usings (`Services.Storage`, `Services.Storage.Utils.Instrumentation`); call `AddInProcessStorageServices` after `AddPublicApiServices`; added `metrics.AddMeter(StorageApiMetrics.MeterName)` to the OpenTelemetry metrics pipeline (the `"ClientManager.StorageApi"` activity source was already added by the API).

### Step 4 (Delete Transport Layer + StorageApi Host)

**Controllers/services cut over to in-process storage:**
- `ClientManager.Api/Services/Implementations/StatisticsService.cs` — fully rewritten. Injects 5 DataAccess interfaces (`IClientConfigurationDatabase`, `IEntityRepository<Service>`, `IEntityRepository<ResourcePool>`, `IResourceAllocationDatabase`, `IGlobalRateLimitDatabase`) + in-process statistics via alias `using StorageStatisticsService = …Api.Services.Storage.Interfaces.IStatisticsService`. Ports the 7 overview/search/detail read-models inline (building anonymous objects → `JsonSerializer.SerializeToElement` for the 3 detail methods; detail not-found throws the public API exceptions); delegates the 6 usage/historical/summaries methods to the in-process statistics service with corrected parameter ordering. Added `using ClientManager.Shared.Contracts.Statistics;` for `IdentifierList`.
- `ClientManager.Api/Controllers/AccessCheckController.cs` — removed `using …Api.Filters;` + `[FailOpenOnError(GrantAccess)]`; kept all `[ProducesResponseType]`.
- `ClientManager.Api/Controllers/ResourceAllocationController.cs` — removed `using …Api.Filters;` + the two `[FailOpenOnError(...)]` attributes; kept all `[ProducesResponseType]`.
- `ClientManager.Api/Middlewares/ErrorHandlingMiddleware.cs` — added `catch (StorageApiProblemException)` → `HandleStorageProblemAsync` before the generic catch so in-process storage problems map to their declared HTTP responses.
- `ClientManager.Api/Program.cs` — removed `using …Api.Filters;`, the `options.Filters.Add<HotPathFailOpenFilter>()` block, the `AddOptions<HotPathResilienceOptions>().Bind(...)`, and `AddStorageApiClients(...)`. Kept `AddPublicApiServices`, `AddInProcessStorageServices`, and the `StorageApiMetrics` meter.
- `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs` — removed `AddStorageApiClients` + `ConfigureClient` + `CreateHandler` helpers and their usings. Kept `AddPublicApiServices` registering all 12 adapters.

**Deleted transport layer + dead config (verified gone):**
- `ClientManager.Api/Services/Internal/` (storage HTTP clients + interfaces), `ClientManager.Api/Utils/StorageApi/` (`StorageApiResilienceHandler`), `ClientManager.Api/Filters/` (`FailOpenOnErrorAttribute.cs` + `HotPathFailOpenFilter.cs`), `ClientManager.Api/Models/Configuration/{HotPathResilienceOptions,StorageApiOptions,StorageApiOptionsValidator}.cs`.
- `ClientManager.Shared/Contracts/Storage/` (`StorageApiRoutes.cs`), `ClientManager.Shared/Models/Problems/StorageProblemResponse.cs`.
- Entire `ClientManager.StorageApi/` project (host).

**Kept (still referenced):** `ClientManager.Shared/Models/Problems/StorageErrorCodes.cs` (used by in-proc exceptions); `ClientManager.Shared/Contracts/Statistics/` (`IdentifierList`, `IdentifierListTypeConverter`, `StatisticsQueryParameters` — used by the public `StatisticsController` + `IStatisticsService`).

**Solution / deployment / tooling / docs:**
- `ClientManager.slnx` — removed the StorageApi `<Project>` entry and the `Quick Access/StorageApi/` folder line.
- `docker-compose.yml` — removed the `storageapi` service; dropped the api's `depends_on: storageapi` + `StorageApi__BaseUrl`; added `volumes: - ./data:/app/data` to api.
- `docker-compose.dev.yml` — moved the `depends_on: redis` override from `storageapi` to `api`.
- `_scripts/configuration.py` — removed `storage_port` runtime key; renamed `storage_api_data_directory` → `api_data_directory` (now `ClientManager.Api/data`) and repointed the `default_history_data_dir` fallback.
- `_scripts/launch_observability_ui.py` — removed the dead `clientmanager-storageapi` Prometheus scrape job + `--storage-host`/`--storage-port` args + `DEFAULT_STORAGE_*`; repointed the storage dashboard panels' `job` selector to `clientmanager-api` (storage metrics are now scraped from the in-process API target); removed the separate StorageApi trace-search URL; updated print-env/appsettings/launchsettings snippets to the single API host.
- `_scripts/{seed_data,traffic_generator,performance_baseline}.py` — updated "start StorageApi first" guidance to the single in-process API host.
- `_scripts/download_images.py` — removed the `ClientManager.StorageApi` Docker image build target (Dockerfile no longer exists).
- `.github/copilot-instructions.md` — Local Testing runbook now Api → AdminUI → seed → traffic (no StorageApi step/port; fixed shutdown order text).
- `README.md` — header/Structure/Architecture (mermaid + request flow)/startup order/Persistence/Repository Layout updated to the single in-process API host.
- `ClientManager.DataAccess/README.md` — topology notes now name `ClientManager.Api` as the only host; startup order Api → AdminUI.
- `.github/testing-checklist.md` — dropped the StorageApi log-tail command + `Storage API unavailable` strings.

## Verification

- `dotnet build ClientManager.Shared.csproj -warnaserror` → 0 warnings, 0 errors.
- `dotnet build ClientManager.slnx` → succeeded (10 pre-existing/unrelated NuGet + CS1573 warnings).
- `git diff --shortstat` → 24 files changed, +55 / -464 (net reduction; new grouped enum files untracked).
- Runtime: StorageApi (5063), Api (5062), AdminUI (5100) all started clean.
- StorageApi JSON logs show `ExtraData.*` structured fields → logger refactor works at runtime.
- Browser UI: Dashboard (`/`) renders live data (25 clients / 20 services / 10 pools); `/services` list renders rows with enum `Status` ("Enabled"). No error banners.

### Step 2 (Data Access Layer)

- `dotnet build ClientManager.DataAccess` → 0 errors (only pre-existing NU1903 Snappier advisory).
- `dotnet run ClientManager.DataAccess.Tests` → "JsonFile storage verification passed." exit 0 — behavior-parity gate green.
- `dotnet build ClientManager.slnx` → 0 errors, 10 pre-existing/unrelated warnings.
- `get_errors` on all 6 edited files → no errors.
- End-to-end CRUD via the running public API (exercises `EntityRepository<T>` + `ClientConfigurationDatabase` write path):
  - Service: POST (409 confirms prior create + conflict path), GET 200 round-trip, PUT 200 (name/enabled edited), DELETE 204, GET-after-delete 404.
  - ResourcePool: POST 201, PUT 200 (maxSlots/ttl edited), DELETE 204.
- `GlobalRateLimitDatabase` composition: `/global-rate-limits/search` → 200, 28 rows with `TargetType`/`TargetId`/`maxRequests` populated.
- Browser UI: `/rate-limits` grid renders rows (grl-notifications/storage/billing) with `Strategy` enum badges + `Window` — confirms composition change end-to-end through the UI.
- `git diff --stat ClientManager.DataAccess/` → 6 files, +142 / −137 (≈neutral, see Finding Dispositions).

### Step 2b (Storage Bindings)

- `dotnet build ClientManager.DataAccess` / `ClientManager.StorageApi` / `ClientManager.Shared` → 0 CS errors (only pre-existing NU1903 Snappier advisory).
- `dotnet run ClientManager.DataAccess.Tests` → "JsonFile storage verification passed." exit 0 — behavior-parity gate green.
- `get_errors` on all 7 edited/new files → no errors.
- **Lucene store** (Development default provider) end-to-end via running public API: CREATE 201, UPDATE 200 (GET reflects rename + `isEnabled:false`), DELETE 204, GET-after-delete 404.
- **JsonFile store** (restarted StorageApi with `Persistence__DefaultProvider=JsonFile`) end-to-end with on-disk `data/services.json` confirmation: CREATE 201 (disk match=1), UPDATE 200 (renamed value on disk=1), DELETE 204 (disk match=0).
- Browser UI `/services`: grid renders live JsonFile-backed data; UI-driven edit of `queue-service` name → grid refreshed, on-disk `data/services.json` updated, value survived full page reload; reverted to seeded "Message Queue".
- `git diff --stat` (Step 2b files) → 11 tracked files, +121 / −131 (net −10); new `StoreSerialization.cs` +16 ⇒ ≈ +6 raw overall (see Finding Dispositions).

### Step 3 (Relocate Storage Services)

- `dotnet build ClientManager.Api` → Build succeeded, 0 errors (after registering the `StorageApiMetrics` singleton + meter that the Storage API host had registered in its own `Program.cs`).
- `dotnet run ClientManager.DataAccess.Tests` → "JsonFile storage verification passed." exit 0 — behavior-parity gate green.
- API host startup (`dotnet run ClientManager.Api`): DI container built and validated (ValidateOnBuild) with no exceptions; in-process hosted services running against JsonFile — logs show `…Services.Storage.Implementations.AllocationCleanupService` ("Allocation counters reconciled", "Expired allocations cleaned up Count=7"), in-process `ResourceAllocationService`, and `…Services.Storage.Utils.Instrumentation.InstrumentedDocumentStore` operations. API listening on 5062 (HTTP 404/405 responses confirm routing).
- Browser UI: Dashboard (`/`) renders live data (25 clients / 20 services / 10 pools) with no error banners — the running system is not broken by the relocation. Controllers still use the HTTP-adapter services this step (cut-over is Step 4), so public endpoints behave exactly as before.
- `git diff --stat` → 49 files changed, +6046 insertions (storage layer duplicated into the API per the plan; the transport layer is removed in Step 4, so the net reduction lands then).

### Step 4 (Delete Transport Layer + StorageApi Host)

- `dotnet build ClientManager.slnx` → Build succeeded, 0 errors (10 pre-existing NU1510/NU1903 warnings).
- `dotnet run ClientManager.DataAccess.Tests` → "JsonFile storage verification passed." exit 0.
- `git show --shortstat e4b2670` → 128 files changed, +683 / −5220 (net −4537 lines in the Step 4 commit).
- Grep (`.cs` / `.csproj` / `.slnx` / `.yml` / `.json`): zero hits for `IRuntimeStateClient`, `IStatisticsReadClient`, `StorageApiRoutes`, `HotPathFailOpen`, `AddStorageApiClients`, `FailOpenOnError`, `ClientManager.StorageApi` project references. Intentional retained names: `StorageApiMetrics`, `StorageApiProblemException`, OpenTelemetry meter/activity source `"ClientManager.StorageApi"`.
- Bookkeeping follow-up (same session): marked Step 4 plan/timeline/overview complete; advanced handoff Current Pass to Step 5; removed orphaned `StorageApi` / `HotPathResilience` appsettings sections and deleted unused `StorageApiUnavailableException.cs`.

### Step 5 (Merged API Services Consolidation)

**New shared infrastructure:**
- `Services/Storage/Implementations/GenericEntityCatalogService.cs` — generic Search/GetById/Create/Update/Delete + cache invalidation for `IEntityRepository<T>` catalogs.
- `Services/Storage/Utils/Instrumentation/StorageHotPathTrace.cs` + `StorageHotPathCompletion` — shared activity/stopwatch envelope (denied/canceled/exception tagging preserved).
- `Services/Storage/Utils/Instrumentation/StorageActivityExtensions.cs` — `StartInternalActivity` helper.
- `Services/Storage/ClientLookupExtensions.cs` — `RequireClientValue` for adapter services.

**Refactored services:**
- `ServiceCatalogService`, `ResourcePoolCatalogService`, `GlobalRateLimitCatalogService` — thin subclasses of `GenericEntityCatalogService` (global limit overrides `CreateAsync` for target uniqueness).
- `ClientConfigurationCatalogService` — `GetSubDocumentAsync` helper for settings lookups; CRUD stays on `IClientConfigurationDatabase` (not `IEntityRepository`).
- `AccessControlService`, `ResourceAllocationService` — `CheckAccessAsync` / `AcquireAsync` / `ReleaseAsync` use `StorageHotPathTrace`; sub-steps use `StartInternalActivity`.
- `RateLimitService` — `StartInternalActivity` for strategy/global-limit reads (existing `TraceRateLimitAsync` retained).
- Public adapters `ClientServiceSettingsService`, `ClientResourcePoolSettingsService`, `ClientGlobalRateLimitService` — `RequireClientValue`.

**Verification:**
- `dotnet build ClientManager.Api` → 0 errors.
- `dotnet run ClientManager.DataAccess.Tests` → passed.
- `git diff --shortstat` (Step 5 storage slice) → net −220 lines on touched files.

### Step 6 (Merged API Controllers) + live verification

**Controllers:**
- `CatalogCrudControllerBase<TEntity>` + `ICatalogCrudService<TEntity>` — shared catalog CRUD; typed `[ProducesResponseType]` on concrete controllers only (C# disallows open generic types in attributes).
- `ServicesController`, `ResourcePoolsController`, `GlobalRateLimitsController` — thin route/tag subclasses.
- `ClientConfigurationSettingsController` — merged the three client settings controllers (same routes preserved).
- Split `StatisticsController` → `StatisticsOverviewController`, `StatisticsCatalogController`, `StatisticsUsageController` (same `/statistics` route prefix).

**Live HTTP smoke (Api @ 5062, AdminUI shell @ 5100 returns 200):**
- Access check, resource acquire, services search, client service settings list, statistics overview → **200**.
- Acquire + release round-trip (Step 5 telemetry path) → **200 / 200 / 200** (release-again `released:false`).
- `swagger/v1/swagger.json` → **200**.

**Note:** No interactive browser automation in this environment; validation used `Invoke-WebRequest` against public API routes the AdminUI calls.

## Finding Dispositions

- **Net-deletion expectation vs. actual (≈neutral +5):** `WON'T FIX BECAUSE` Step 3 mandates converting `GlobalRateLimitDatabase` from inheritance to composition, which legitimately *adds* six explicit `IEntityRepository` delegation one-liners. The step's substantive goal — removing copy-paste `DocumentQuery` building and the mirrored sub-document mutation blocks — was achieved (helpers `BuildTargetQuery`, `BuildQuery`, `MutateAsync`, `ForEachAllocationKey`). The composition boilerplate offsets those deletions, yielding a roughly flat diff. Forcing artificial deletions would contradict the mandated design.
- **`UsageSnapshotDatabase` constructor not converted to primary constructor:** `ALREADY SATISFIED` (intentional) — left as a classic constructor to avoid disturbing an unrelated pre-existing mis-indented region; the query-builder dedup (the actual Step 5 goal) was still completed.
- **Step 2b net line count ≈neutral (+6 raw) rather than strongly negative:** `WON'T FIX BECAUSE` the substantive win is structural — four duplicated `JsonOptions` field definitions collapsed into one shared 16-line helper, the factory's certificate/Mongo/Redis construction extracted into named single-purpose methods, and six option classes converted to records. Extracting factory helpers adds method-signature scaffolding that offsets the deleted inline code, and the new shared helper file is additive. Forcing artificial deletions would contradict the plan's stated goal ("cleaner shared structure") and harm readability.
- **Step 2b on-disk round-trip required a provider override:** `ALREADY SATISFIED` — the Development environment pins `Persistence__DefaultProvider=Lucene`, so the default local run validated the refactored `LuceneDocumentStore` (full CRUD). StorageApi was restarted with `Persistence__DefaultProvider=JsonFile` to satisfy the plan's explicit JsonFile on-disk round-trip; both refactored stores are therefore covered end-to-end.
- **Step 3 large positive diff (+6046):** `WON'T FIX BECAUSE` this step intentionally *adds* the relocated in-process storage layer alongside the existing HTTP transport path so the API keeps compiling and running. The plan explicitly defers the deletion of the old transport layer (storage clients, adapters, and the duplicated source it replaces) to Step 4, where the net reduction materializes. A negative diff here would require deleting the transport path prematurely and break the running system.
- **`StorageApiMetrics` singleton + meter not in the copied source:** `FIXED` — in the Storage API these were registered in its host `Program.cs`, not in `AddStorageApi`, so the relocated services failed DI validation (`Unable to resolve StorageApiMetrics`). Registered the singleton inside `AddInProcessStorageServices` and added `metrics.AddMeter(StorageApiMetrics.MeterName)` to the API's OpenTelemetry pipeline, preserving metric/activity behavior.
- **Storage services duplicated rather than shared (Option A):** `ALREADY SATISFIED` (user-confirmed decision, logged in `decision-log.md`) — duplicating into `ClientManager.Api.Services.Storage.*` keeps the Storage API host intact and avoids name collisions with the existing API HTTP-adapter services (same type names live in `ClientManager.Api.Services.Interfaces`). The duplication is temporary; Step 4 removes the transport layer.
- **Step 4 large negative diff (−4537 in commit):** `ALREADY SATISFIED` — deleting the StorageApi project, internal HTTP clients, transport contracts, resilience/fail-open plumbing, and duplicated host code materializes the plan's headline LOC win. Step 3's +6046 was expected temporary growth.
- **Orphaned appsettings / exception after Step 4 commit:** `FIXED` during bookkeeping — `StorageApi` and `HotPathResilience` config blocks and `StorageApiUnavailableException` survived the main commit because options/DI registration were removed first; config and the unused exception type are now deleted.

## Blockers

None.

## Workflow friction

- Plan claims logger overload collapse is source-compatible, but actual call sites pass `Exception` as the second positional argument. Single-signature `(message, object? extraData, Exception? exception)` would silently route exceptions into `extraData`. Mitigation: update the ~8 exception call sites to keep correct binding.
