# Plan: API Standards — Step 1: API Versioning

> **Status**: ✅ Completed
> **Prerequisite**: None — this is the first step.
> **Next**: [api-standards-2-pagination-foundation.md](api-standards-2-pagination-foundation.md)
> **Parent**: [api-standards-overview.md](api-standards-overview.md)

## TL;DR

Install `Asp.Versioning.Mvc` packages, configure API versioning with URL segment strategy where the default version is loaded from `appsettings.json`, update all controller `[Route]` attributes to `api/v{version:apiVersion}/...`, and configure Swagger to support version switching. MetricsController moves under `api/v1/metrics/`.

## Reference Pattern

In [ClientManager.Api/Controllers/ClientConfigurationsController.cs](ClientManager.Api/Controllers/ClientConfigurationsController.cs):
- Controllers use `[ApiController]` and `[Route("api/clients")]` 
- Standard REST pattern with `[Tags(...)]` for Swagger grouping

In [ClientManager.Api/Program.cs](ClientManager.Api/Program.cs):
- Swagger configured with `AddSwaggerGen`, `UseSwagger`, `UseSwaggerUI`
- XML comments included via `IncludeXmlComments`
- Custom `TagDescriptionsDocumentFilter` applied

## Steps

### 1. Install NuGet packages

Add `Asp.Versioning.Mvc` and `Asp.Versioning.Mvc.ApiExplorer` to `ClientManager.Api.csproj`.

### 2. Add versioning configuration to `appsettings.json`

Add an `ApiVersioning` section to `appsettings.json` with a `DefaultVersion` field (e.g., `"1.0"`). This is where the default API version is read from at startup — not hardcoded in `Program.cs`.

### 3. Configure API versioning services in `Program.cs`

After `AddControllers()`, register API versioning with `AddApiVersioning` + `AddApiExplorer`. Read `DefaultApiVersion` from the config section added in Step 2. Use `UrlSegmentApiVersionReader`, enable `ReportApiVersions`, and set `AssumeDefaultVersionWhenUnspecified = true`. Chain `.AddApiExplorer()` with `SubstituteApiVersionInUrl = true` and `GroupNameFormat = "'v'VVV"`.

### 4. Update SwaggerGen to support versioned documents

Replace the current `AddSwaggerGen` call to register a named doc `"v1"` with an `OpenApiInfo` title and version. Keep the existing XML comment inclusion and `TagDescriptionsDocumentFilter`.

### 5. Update SwaggerUI endpoint

Update the `UseSwaggerUI` call to point at `/swagger/v1/swagger.json` with label `"ClientManager API v1"`.

### 6. Update all controller routes

Change `[Route("api/...")]` to `[Route("api/v{version:apiVersion}/...")]` on every controller. Add `[ApiVersion("1.0")]` to each.

| Controller | Old Route | New Route |
|---|---|---|
| ClientConfigurationsController | `api/clients` | `api/v{version:apiVersion}/clients` |
| ResourcePoolsController | `api/resource-pools` | `api/v{version:apiVersion}/resource-pools` |
| ServicesController | `api/services` | `api/v{version:apiVersion}/services` |
| GlobalRateLimitsController | `api/global-rate-limits` | `api/v{version:apiVersion}/global-rate-limits` |
| AccessCheckController | `api/access` | `api/v{version:apiVersion}/access` |
| ResourceAllocationController | `api/resources` | `api/v{version:apiVersion}/resources` |
| StatisticsController | `api/statistics` | `api/v{version:apiVersion}/statistics` |
| MetricsController | `/prometheus`, `/grafana` (root-level absolute) | `api/v{version:apiVersion}/metrics` |

### 7. Update MetricsController route and actions

MetricsController currently uses absolute root paths (`[HttpGet("/prometheus")]`, `[HttpGet("/grafana")]`) with no `[Route]` on the controller. Add a controller-level `[Route("api/v{version:apiVersion}/metrics")]` and change the action routes to relative paths (`[HttpGet("prometheus")]`, `[HttpGet("grafana")]`). Final paths: `api/v1/metrics/prometheus` and `api/v1/metrics/grafana`.

## Verification

- Project compiles without errors.
- `dotnet build` succeeds for `ClientManager.Api`.
- Swagger at `/docs` loads and shows "ClientManager API v1" in the version selector.
- All endpoints respond at their new `api/v1/...` paths (e.g., `GET /api/v1/clients`).
- Old `api/clients` path returns 404 (no fallback).
- `GET /api/v1/metrics/prometheus` returns Prometheus exposition format text.
- `GET /api/v1/metrics/grafana` returns Grafana JSON metrics.
- Old `/prometheus` and `/grafana` root paths return 404.
- Prometheus/Grafana scraper configs will need updating to the new paths.
- **UI: The AdminUI will NOT work after this step** — that's expected and handled in Step 4.
