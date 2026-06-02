# Implementation Handoff — code-slimdown

## Current Pass

Step 2: Data Access Layer. Slimming `ClientManager.DataAccess`:
- Modernize `RateLimitStateDatabase` (primary constructor + expression-bodied).
- Modernize `EntityRepository<T>` (genuine polymorphic seam: Service, ResourcePool, base of GlobalRateLimit).
- Convert `GlobalRateLimitDatabase` from inheritance to composition + `BuildTargetQuery` helper.
- Extract sub-document mutation helper in `ClientConfigurationDatabase`.
- Extract query-builder helper in `UsageSnapshotDatabase`.
- Dedupe counter-key construction in `ResourceAllocationDatabase`.

## Pass History

| Pass | Date | Summary |
| --- | --- | --- |
| 1 | 2026-06-02 | Step 1 implemented and verified (build + UI) |
| 2 | 2026-06-02 | Step 2 (Data Access Layer) implemented and verified (build + tests + CRUD + UI) |

## Changed Files

- `ClientManager.Shared/Models/Problems/ProblemResponse.cs` — class → record.
- `ClientManager.Shared/Models/Problems/StorageProblemResponse.cs` — class → record.
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

## Finding Dispositions

- **Net-deletion expectation vs. actual (≈neutral +5):** `WON'T FIX BECAUSE` Step 3 mandates converting `GlobalRateLimitDatabase` from inheritance to composition, which legitimately *adds* six explicit `IEntityRepository` delegation one-liners. The step's substantive goal — removing copy-paste `DocumentQuery` building and the mirrored sub-document mutation blocks — was achieved (helpers `BuildTargetQuery`, `BuildQuery`, `MutateAsync`, `ForEachAllocationKey`). The composition boilerplate offsets those deletions, yielding a roughly flat diff. Forcing artificial deletions would contradict the mandated design.
- **`UsageSnapshotDatabase` constructor not converted to primary constructor:** `ALREADY SATISFIED` (intentional) — left as a classic constructor to avoid disturbing an unrelated pre-existing mis-indented region; the query-builder dedup (the actual Step 5 goal) was still completed.

## Blockers

None.

## Workflow friction

- Plan claims logger overload collapse is source-compatible, but actual call sites pass `Exception` as the second positional argument. Single-signature `(message, object? extraData, Exception? exception)` would silently route exceptions into `extraData`. Mitigation: update the ~8 exception call sites to keep correct binding.
