# Implementation Handoff

## Current Pass

- Pass type: Implementation (delegated mode)
- Authoring agent: @Implement
- Plan step: api-cr-remediation-2-http-exception-pipeline.md
- Branch: feature/api-cr-remediation-http-problems (created from feature/api-cr-remediation-foundation tip f458b78); committed by @Inscribe in the initial Step 2 pass
- Summary: Introduced an abstract `HttpProblemException` base carrying status code, problem title, public detail, and optional retry-after. Refactored every expected mapped exception to derive from it. Pushed the mandatory not-found decision for the four top-level resources (client, service, resource pool, global rate limit) into the internal client boundary so controllers no longer inspect nullability. Collapsed `ErrorHandlingMiddleware` to a single problem path (warn-level) plus an unexpected-defect path (error-level/500), preserving retry-after headers.

## Files Changed

| Path | Intent | Verification impact |
|------|--------|---------------------|
| ClientManager.Api/Models/Exceptions/HttpProblemException.cs | New abstract base contract (StatusCode, Title, RetryAfterSeconds) | Compiled |
| ClientManager.Api/Models/Exceptions/NotFoundException.cs | Derive from base (404 / "Not Found") | Compiled |
| ClientManager.Api/Models/Exceptions/ConflictException.cs | Derive from base (409 / "Conflict") | Compiled |
| ClientManager.Api/Models/Exceptions/ValidationException.cs | Derive from base (400 / "Bad Request") | Compiled |
| ClientManager.Api/Models/Exceptions/RateLimitedException.cs | Derive from base (429), retry-after via base | Compiled |
| ClientManager.Api/Models/Exceptions/StorageApiUnavailableException.cs | Derive from base (503), retry-after via base | Compiled |
| ClientManager.Api/Models/Exceptions/AccessNotConfiguredException.cs | Derive from base (401 / "Unauthorized") | Compiled |
| ClientManager.Api/Models/Exceptions/AccessDeniedException.cs | Derive from base (403 / "Forbidden") | Compiled |
| ClientManager.Api/Models/Exceptions/ClientDisabledException.cs | Derive from base (403 / "Forbidden") | Compiled |
| ClientManager.Api/Models/Exceptions/ServiceDisabledException.cs | Derive from base (403 / "Forbidden") | Compiled |
| ClientManager.Api/Middlewares/ErrorHandlingMiddleware.cs | One HttpProblemException path (warn) + unexpected path (error/500); retry-after preserved | Compiled |
| ClientManager.Api/Services/InternalClients/Interfaces/Configuration/IServiceCatalogClient.cs | `GetByIdAsync` now non-nullable | Compiled |
| ClientManager.Api/Services/InternalClients/Interfaces/Configuration/IResourcePoolCatalogClient.cs | `GetByIdAsync` now non-nullable | Compiled |
| ClientManager.Api/Services/InternalClients/Interfaces/Configuration/IGlobalRateLimitCatalogClient.cs | `GetByIdAsync` now non-nullable | Compiled |
| ClientManager.Api/Services/InternalClients/Interfaces/Configuration/IClientConfigurationStoreClient.cs | `GetByIdAsync` now non-nullable | Compiled |
| ClientManager.Api/Services/InternalClients/Implementations/Configuration/ServiceCatalogClient.cs | Throws ServiceNotFoundException at boundary | Compiled |
| ClientManager.Api/Services/InternalClients/Implementations/Configuration/ResourcePoolCatalogClient.cs | Throws ResourcePoolNotFoundException at boundary | Compiled |
| ClientManager.Api/Services/InternalClients/Implementations/Configuration/GlobalRateLimitCatalogClient.cs | Throws GlobalRateLimitNotFoundException at boundary | Compiled |
| ClientManager.Api/Services/InternalClients/Implementations/Configuration/ClientConfigurationStoreClient.cs | Throws ClientNotFoundException at boundary; removed now-unused GetOptionalAsync helper | Compiled |
| ClientManager.Api/Controllers/ServicesController.cs | Removed `?? throw`; dropped unused Exceptions using | Compiled |
| ClientManager.Api/Controllers/ResourcePoolsController.cs | Removed `?? throw`; dropped unused Exceptions using | Compiled |
| ClientManager.Api/Controllers/GlobalRateLimitsController.cs | Removed `?? throw`; dropped unused Exceptions using | Compiled |
| ClientManager.Api/Controllers/ClientConfigurationsController.cs | Removed `?? throw`; dropped unused Exceptions using | Compiled |

## Verification

| Check | Method | Result | Evidence |
|-------|--------|--------|----------|
| Build | `dotnet build ClientManager.Api/ClientManager.Api.csproj` | PASS | 0 errors; 1 pre-existing warning in ClientManager.Shared/Logging/AppLogger.cs (CS8604, unrelated/out of scope) |
| Edited-file diagnostics | get_errors on changed files | PASS | No errors found |
| Live HTTP 404/409/503 + UI outage flows | Deferred | DEFERRED | Orchestrator runtime verification (see Risks) |

## Review Responses

| Finding ID | Disposition | Evidence | Notes |
|------------|-------------|----------|-------|
| (none yet) | n/a | n/a | First implementation pass; no review findings received. |

## Risks And Follow-Ups

- Runtime verification deferred: live 404 (`GET /clients/{missing}`), 409 (duplicate create), and 503 (storage host down) RFC 7807 responses plus `/clients`, `/services`, `/monitor` UI outage/recovery flows were NOT exercised in this pass per delegated scope. Residual risk that response bodies/headers differ at runtime is low — titles/details/status are now sourced directly from the typed exceptions the middleware already produced.
- `ClientConfigurationsController` Update/Delete still do not declare or produce 404 (storage 404 surfaces via `SendAsync` as an unexpected exception). This is unchanged pre-existing behavior; the route contract there has no `[ProducesResponseType(404)]`, so it was intentionally left out of scope for this step.
- Pre-existing CS8604 warning in `ClientManager.Shared/Logging/AppLogger.cs` is outside this step's edit scope and was not touched.

## Pass History

| Pass | Commit | Summary |
|------|--------|---------|
| 1 | (uncommitted) | Step 2 HTTP exception pipeline: base problem contract, boundary-owned 404s, simplified middleware. Build green. |
