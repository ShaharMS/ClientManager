# Implementation Handoff — code-slimdown

## Current Pass

Step 1: Shared Foundation. Slimming `ClientManager.Shared`:
- Convert `ProblemResponse` / `StorageProblemResponse` to records.
- Convert `UserPreferences` (AdminUI) to a record.
- Collapse `IAppLogger<T>` / `AppLogger<T>` overloads to one method per level.
- Consolidate single-enum files into grouped files (same namespaces).
- Template-ize `StorageApiRoutes`.

## Pass History

| Pass | Date | Summary |
| --- | --- | --- |
| 1 | 2026-06-02 | Step 1 implemented and verified (build + UI) |

## Changed Files

- `ClientManager.Shared/Models/Problems/ProblemResponse.cs` — class → record.
- `ClientManager.Shared/Models/Problems/StorageProblemResponse.cs` — class → record.
- `ClientManager.AdminUI/Models/UserPreferences.cs` — class → record (kept `set` accessors; Settings.razor mutates).
- `ClientManager.Shared/Logging/IAppLogger.cs` — one signature per level `(message, extraData, exception)`.
- `ClientManager.Shared/Logging/AppLogger.cs` — expression-bodied members for reduced interface.
- Logger call-site fixes (exception binding): RuntimeStateClient, Api/StorageApi ErrorHandlingMiddleware, InstrumentedDocumentStore, UsagePersistenceService, RateLimitService, AllocationCleanupService, AccessControlService, ResourceAllocationService, HotPathFailOpenFilter.
- Enum consolidation: created `Models/Enums/StorageEnums.cs`, `RateLimitEnums.cs`, `UsageEnums.cs`, `Models/Search/SearchEnums.cs`; deleted 8 single-enum files. Namespaces unchanged.
- `ClientManager.Shared/Contracts/Storage/StorageApiRoutes.cs` — added private `Escape` helper; routes identical.

## Verification

- `dotnet build ClientManager.Shared.csproj -warnaserror` → 0 warnings, 0 errors.
- `dotnet build ClientManager.slnx` → succeeded (10 pre-existing/unrelated NuGet + CS1573 warnings).
- `git diff --shortstat` → 24 files changed, +55 / -464 (net reduction; new grouped enum files untracked).
- Runtime: StorageApi (5063), Api (5062), AdminUI (5100) all started clean.
- StorageApi JSON logs show `ExtraData.*` structured fields → logger refactor works at runtime.
- Browser UI: Dashboard (`/`) renders live data (25 clients / 20 services / 10 pools); `/services` list renders rows with enum `Status` ("Enabled"). No error banners.

## Finding Dispositions

None yet.

## Blockers

None.

## Workflow friction

- Plan claims logger overload collapse is source-compatible, but actual call sites pass `Exception` as the second positional argument. Single-signature `(message, object? extraData, Exception? exception)` would silently route exceptions into `extraData`. Mitigation: update the ~8 exception call sites to keep correct binding.
