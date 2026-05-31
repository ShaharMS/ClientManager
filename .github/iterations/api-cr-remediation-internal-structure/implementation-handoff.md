# Implementation Handoff

## Current Pass

- Pass type: implementation (delegated)
- Authoring agent: @Implement
- Plan step: api-cr-remediation-3-internal-transport-structure.md
- Branch: feature/api-cr-remediation-http-problems (built on tip d0db01e)
- Summary: Flattened and renamed the internal storage client structure. `Services/InternalClients` → `Services/Internal` with `Interfaces/` and `Implementations/` (dropped the `Configuration` sub-nesting). Transport helpers (`StorageApiResilienceHandler`, `StorageApiResilienceState`, `StorageApiResponseReader`) moved to a dedicated `Utils/StorageApi` area. Added a declared retryability contract (`StorageApiRequestOptions.Retryable` + `PostRetryableAsJsonAsync`) and removed the `POST + /search` URI-suffix heuristic. Renamed `AddClientManager` → `AddPublicApiServices`, merged the storage-client registration extension into `ServiceCollectionExtensions`. Added XML docs / `<inheritdoc />` to interfaces and helpers, renamed `emptyMessage` → `missingPayloadErrorMessage`, and replaced `Func<Exception>` factory parameters with eager `Exception` instances.

## Files Changed

| Path | Intent | Verification impact |
|------|--------|---------------------|
| ClientManager.Api/Utils/StorageApi/StorageApiRequestOptions.cs | New: declared `Retryable` request option key | Build |
| ClientManager.Api/Utils/StorageApi/StorageApiRequestExtensions.cs | New: `PostRetryableAsJsonAsync` marks request retryable | Build |
| ClientManager.Api/Utils/StorageApi/StorageApiResilienceHandler.cs | Moved from InternalClients; replaced `/search` heuristic with declared `Retryable` option; clones options on retry; docs | Build |
| ClientManager.Api/Utils/StorageApi/StorageApiResilienceState.cs | Moved; added XML docs (behavior unchanged) | Build |
| ClientManager.Api/Utils/StorageApi/StorageApiResponseReader.cs | Moved; `emptyMessage` → `missingPayloadErrorMessage`; docs | Build |
| ClientManager.Api/Services/Internal/Interfaces/*.cs (6 files) | Moved/flattened; full per-method XML docs | Build |
| ClientManager.Api/Services/Internal/Implementations/*.cs (6 files) | Moved/flattened; `<inheritdoc />`; retryable search; `Func<Exception>` → `Exception` | Build |
| ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs | Renamed `AddClientManager` → `AddPublicApiServices`; merged `AddStorageApiClients`; docs | Build |
| ClientManager.Api/Utils/Extensions/StorageApiClientServiceCollectionExtensions.cs | Deleted (merged) | Build |
| ClientManager.Api/Program.cs | `AddClientManager()` → `AddPublicApiServices()` | Build |
| ClientManager.Api/Controllers/*.cs (9 files) | Updated usings to `Services.Internal.Interfaces` | Build |
| ClientManager.Api/Services/Implementations/{AccessControlService,ResourceAllocationService}.cs | Updated usings to `Services.Internal.Interfaces` | Build |
| ClientManager.Api/Services/InternalClients/ (dir) | Deleted (replaced by Internal + Utils/StorageApi) | Build |

## Verification

| Check | Method | Result | Evidence |
|-------|--------|--------|----------|
| API compiles | `dotnet build ClientManager.Api/ClientManager.Api.csproj` | Pass | Build succeeded, 0 errors |
| Workspace type-check | `dotnet build ClientManager.slnx` | Pass | Build succeeded, 0 errors |
| Edited-file diagnostics | get_errors on touched files | Pass | No errors |
| Reconstructed RuntimeStateClient parity | `git diff` vs HEAD original | Pass | Only namespace/using/doc lines changed |

## Review Responses

| Finding ID | Disposition | Evidence | Notes |
|------------|-------------|----------|-------|
| (none filed) | — | — | No review-packet findings at time of pass |

## Risks And Follow-Ups

- Live DI resolution, `/docs` (Swagger), and Admin UI page checks deferred to orchestrator runtime verification — not exercised in this delegated build-only pass.
- `RuntimeStateClient` retains pre-existing inline `// CR: Use fluent API` comments. These target a later services/observability step, not this structural step; left intact (won't-fix-here).

## Pass History

| Pass | Commit | Summary |
|------|--------|---------|
| 1 | (uncommitted) | Internal transport structure flatten + rename + declared retryability |
