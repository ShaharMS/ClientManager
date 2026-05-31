# Implementation Handoff

## Current Pass

- Pass type: Step 4 implementation (delegated)
- Authoring agent: Implement agent (delegated)
- Plan step: api-cr-remediation-4-services-and-controllers.md
- Branch: feature/api-cr-remediation-services-controllers
- Summary: Introduced public API service interfaces + implementations for the six direct-internal-client controller domains (client configuration + 3 nested settings, services catalog, resource-pool catalog, global-rate-limit catalog, statistics read, metrics read), registered them in `AddPublicApiServices`, and migrated every affected controller to inject the public interface. Controllers now only bind/normalize inputs and delegate; storage-side `Services.Internal...` clients are no longer injected into any controller. Removed controller-side null checks (rely on Step 2 typed-exception pipeline), standardized response locals to domain-named variables, moved `IdentifierList.HasValues` normalization and client-summary pagination out of `StatisticsController` into `StatisticsService`, and aligned `[ProducesResponseType]` 404 annotations on nested settings Set/Remove + client-config Update/Delete.

## Files Changed

| Path | Intent | Verification impact |
|------|--------|---------------------|
| ClientManager.Api/Services/Interfaces/IClientConfigurationService.cs | New public interface | Compiles |
| ClientManager.Api/Services/Interfaces/IClientServiceSettingsService.cs | New public interface | Compiles |
| ClientManager.Api/Services/Interfaces/IClientResourcePoolSettingsService.cs | New public interface | Compiles |
| ClientManager.Api/Services/Interfaces/IClientGlobalRateLimitService.cs | New public interface | Compiles |
| ClientManager.Api/Services/Interfaces/IServiceCatalogService.cs | New public interface | Compiles |
| ClientManager.Api/Services/Interfaces/IResourcePoolCatalogService.cs | New public interface | Compiles |
| ClientManager.Api/Services/Interfaces/IGlobalRateLimitCatalogService.cs | New public interface | Compiles |
| ClientManager.Api/Services/Interfaces/IStatisticsService.cs | New public interface (owns IdentifierList normalization + paging) | Compiles |
| ClientManager.Api/Services/Interfaces/IMetricsService.cs | New public interface | Compiles |
| ClientManager.Api/Services/Implementations/ClientConfigurationService.cs | New adapter onto internal store client | Compiles |
| ClientManager.Api/Services/Implementations/ClientServiceSettingsService.cs | New adapter; single-get throws typed NotFound | Compiles |
| ClientManager.Api/Services/Implementations/ClientResourcePoolSettingsService.cs | New adapter; single-get throws typed NotFound | Compiles |
| ClientManager.Api/Services/Implementations/ClientGlobalRateLimitService.cs | New adapter; single-get throws typed NotFound | Compiles |
| ClientManager.Api/Services/Implementations/ServiceCatalogService.cs | New catalog adapter | Compiles |
| ClientManager.Api/Services/Implementations/ResourcePoolCatalogService.cs | New catalog adapter | Compiles |
| ClientManager.Api/Services/Implementations/GlobalRateLimitCatalogService.cs | New catalog adapter | Compiles |
| ClientManager.Api/Services/Implementations/StatisticsService.cs | New adapter; ResolveOptionalIds + ToPagedResponse | Compiles |
| ClientManager.Api/Services/Implementations/MetricsService.cs | New adapter | Compiles |
| ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs | Register 9 new services (AddScoped) | Compiles |
| ClientManager.Api/Controllers/ClientConfigurationsController.cs | Migrate to IClientConfigurationService; named locals; 404/409 | Compiles |
| ClientManager.Api/Controllers/ClientConfigurationServicesController.cs | Migrate to IClientServiceSettingsService; drop null checks; 404 | Compiles |
| ClientManager.Api/Controllers/ClientConfigurationResourcePoolsController.cs | Migrate to IClientResourcePoolSettingsService; drop null checks; 404 | Compiles |
| ClientManager.Api/Controllers/ClientConfigurationGlobalRateLimitController.cs | Migrate to IClientGlobalRateLimitService; drop null check | Compiles |
| ClientManager.Api/Controllers/ServicesController.cs | Migrate to IServiceCatalogService; named locals | Compiles |
| ClientManager.Api/Controllers/ResourcePoolsController.cs | Migrate to IResourcePoolCatalogService; named locals | Compiles |
| ClientManager.Api/Controllers/GlobalRateLimitsController.cs | Migrate to IGlobalRateLimitCatalogService; named locals | Compiles |
| ClientManager.Api/Controllers/StatisticsController.cs | Migrate to IStatisticsService; remove inline helpers; named locals | Compiles |
| ClientManager.Api/Controllers/MetricsController.cs | Migrate to IMetricsService | Compiles |

## Verification

| Check | Method | Result | Evidence |
|-------|--------|--------|----------|
| API builds | `dotnet build ClientManager.Api/ClientManager.Api.csproj` | Pass | "Build succeeded with 1 warning(s)"; only warning is pre-existing CS8604 in ClientManager.Shared/AppLogger.cs (out of scope) |
| No controller injects internal transport client | grep `Services\.Internal\|StoreClient\|CatalogClient\|ReadClient` in `ClientManager.Api/Controllers/**` | Pass | No matches |
| Live UI page checks | Deferred to orchestrator runtime verification | Not run | Delegated mode; runtime stack not started |

## Review Responses

| Finding ID | Disposition | Evidence | Notes |
|------------|-------------|----------|-------|
| (none supplied) | n/a | n/a | No review findings were provided for this pass. |

## Risks And Follow-Ups

- Catalog `Update` services preserve prior behavior by sending the original entity to the storage client and returning `entity with { Id = id }`; no semantic change intended.
- `StatisticsService` consumes `IdentifierList` (a `ClientManager.Shared` contract) to centralize `HasValues` normalization, since Shared is out of scope to edit.
- Live UI verification against the AdminUI dashboard was not performed (delegated mode, no runtime stack); orchestrator should confirm statistics/metrics dashboards still render.

## Pass History

| Pass | Commit | Summary |
|------|--------|---------|
| 1 | (uncommitted) | Created 9 public services + DI registration; migrated all 9 direct-internal-client controllers; build green; grep proof clean. |
