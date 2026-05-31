# Implementation Handoff

## Current Pass

- Pass type: Implementation (Step 1 — foundation contracts and options)
- Authoring agent: @Implement (delegated mode)
- Plan step: api-cr-remediation-1-foundation-contracts.md
- Branch: feature/api-cr-remediation-foundation
- Summary: Extracted immutable cross-host route/query contracts into `ClientManager.Shared`, replaced controller-local `ParseIds`/`ParseClientIds` with a reusable `IdentifierList` value object bound via a `TypeConverter`, and introduced documented typed options (`ApiVersioningSettings`, `ObservabilityOptions`) plus `IValidateOptions<T>` validators for `StorageApiOptions`, API versioning, and observability. Only `ClientManager.Api` and `ClientManager.Shared` were touched.

## Files Changed

| Path | Intent | Verification impact |
|------|--------|---------------------|
| ClientManager.Shared/Contracts/Statistics/StatisticsQueryParameters.cs | New: canonical statistics query-parameter name constants | Compiled into Shared |
| ClientManager.Shared/Contracts/Statistics/IdentifierList.cs | New: documented comma-separated identifier value object (one parsing rule) | Compiled into Shared |
| ClientManager.Shared/Contracts/Statistics/IdentifierListTypeConverter.cs | New: TypeConverter enabling model binding to IdentifierList | Compiled into Shared |
| ClientManager.Shared/Contracts/Storage/StorageApiRoutes.cs | New (moved from Api): documented immutable storage route fragments + query assembly using shared param-name constants | Compiled into Shared |
| ClientManager.Api/Services/InternalClients/StorageApiRoutes.cs | Deleted: moved to Shared | Removed from Api |
| ClientManager.Api/Controllers/StatisticsController.cs | Replaced string id params + ParseIds/ParseClientIds helpers with IdentifierList binding | Build + statistics binding |
| ClientManager.Api/Services/InternalClients/Implementations/StatisticsReadClient.cs | Added using for shared StorageApiRoutes | Build |
| ClientManager.Api/Services/InternalClients/Implementations/RuntimeStateClient.cs | Added using for shared StorageApiRoutes | Build |
| ClientManager.Api/Services/InternalClients/Implementations/Configuration/ClientConfigurationStoreClient.cs | Added using for shared StorageApiRoutes | Build |
| ClientManager.Api/Services/InternalClients/Implementations/Configuration/ServiceCatalogClient.cs | Added using for shared StorageApiRoutes | Build |
| ClientManager.Api/Services/InternalClients/Implementations/Configuration/ResourcePoolCatalogClient.cs | Added using for shared StorageApiRoutes | Build |
| ClientManager.Api/Services/InternalClients/Implementations/Configuration/GlobalRateLimitCatalogClient.cs | Added using for shared StorageApiRoutes | Build |
| ClientManager.Api/Models/Configuration/StorageApiOptionsValidator.cs | New: IValidateOptions<StorageApiOptions> replacing inline .Validate chain | Build + startup validation |
| ClientManager.Api/Models/Configuration/ApiVersioningSettings.cs | New: typed ApiVersioning settings | Build |
| ClientManager.Api/Models/Configuration/ApiVersioningSettingsValidator.cs | New: IValidateOptions<ApiVersioningSettings> | Build + startup validation |
| ClientManager.Api/Models/Configuration/ObservabilityOptions.cs | New: typed Observability settings | Build |
| ClientManager.Api/Models/Configuration/ObservabilityOptionsValidator.cs | New: IValidateOptions<ObservabilityOptions> | Build + startup validation |
| ClientManager.Api/Utils/Extensions/StorageApiClientServiceCollectionExtensions.cs | Replaced inline .Validate chain with registered validator | Build + startup validation |
| ClientManager.Api/Program.cs | Bound typed ApiVersioning + Observability options with validators; read values from typed settings | Build + startup |
## Verification

| Check | Method | Result | Evidence |
|-------|--------|--------|----------|
| Shared compiles | `dotnet build ClientManager.Shared/ClientManager.Shared.csproj` | Pass | Build succeeded; 1 pre-existing warning CS8604 in Logging/AppLogger.cs (untouched) |
| Api compiles | `dotnet build ClientManager.Api/ClientManager.Api.csproj` | Pass | Build succeeded; 0 warnings, 0 errors |
| No unsafe type escapes introduced | Manual review | Pass | No any-equivalent casts; converter uses base fallbacks |
| Runtime / browser UI checks (/monitor, /services, /resource-pools, /clients/{id}) | Not run | Deferred | Delegated build-only pass; orchestrator/reviewer to exercise UI |
## Review Responses

| Finding ID | Disposition | Evidence | Notes |
|------------|-------------|----------|-------|

## Risks And Follow-Ups

- Statistics query binding now flows through `IdentifierList` + `TypeConverter`. Builds pass, but query-parameter binding behavior should be confirmed at runtime via `/docs` and the `/monitor` page (deferred — not run in this delegated build-only pass).
- `StorageApiRoutes` moved to `ClientManager.Shared.Contracts.Storage` and is now `public`. `ClientManager.StorageApi` was intentionally NOT updated to consume it (out of scope); a future parallel CR can point the storage host at the same shared contract.
- The `// CR: Place in configuration` markers on route fragments were removed deliberately: the overview Key Decision supersedes them (immutable routes belong in Shared, not appsettings).
- Step 3 (internal transport structure) will further reorganize the transport layer; the shared route move was kept name-stable (`StorageApiRoutes`) to minimize churn there.

## Pass History

| Pass | Commit | Summary |
|------|--------|---------|
| 1 | (pending @Inscribe) | Step 1 foundation contracts + options: shared route/query contracts, IdentifierList binder, typed options + validators. Shared + Api build green. |