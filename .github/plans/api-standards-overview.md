# Plan: API Standards — Versioning, Pagination & Filtering

## Status: 🔲 Not started

## Overview

The ClientManager API currently serves all endpoints under unversioned `api/` routes, returns unbounded collections from all list endpoints, and has no standardized approach to query filtering. This plan brings the API in line with clean REST standards by introducing three sequential changes:

1. **API Versioning** — All endpoints move to `api/v1/` with Swagger version switching support.
2. **Consistent Pagination** — Every collection-returning endpoint uses a standard `PagedResponse<T>` envelope with `page`/`pageSize` query params. No endpoint may return an unbound enumerable.
3. **Query Param Filtering** — List endpoints gain consistent optional filter parameters (e.g., `?isEnabled=true`). Filtering is never encoded in the URL path.

The current route structure is `api/clients`, `api/services`, `api/resource-pools`, `api/global-rate-limits`, `api/access`, `api/resources`, `api/statistics`, plus root-level `/prometheus` and `/grafana`. The AdminUI project has 5 API service classes (`ClientApiService`, `ServiceApiService`, `ResourcePoolApiService`, `GlobalRateLimitApiService`, `StatisticsApiService`) that hardcode these paths.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [api-standards-1-versioning.md](.github/plans/api-standards-1-versioning.md) | Install versioning packages, configure services, update all controller routes to `api/v1/`, configure Swagger version switching |
| 2 | [api-standards-2-pagination-foundation.md](.github/plans/api-standards-2-pagination-foundation.md) | Create standard `PagedRequest`/`PagedResponse<T>` models and pagination extension method |
| 3 | [api-standards-3-apply-pagination-filtering.md](.github/plans/api-standards-3-apply-pagination-filtering.md) | Apply pagination and query param filtering to all collection-returning controller endpoints |
| 4 | [api-standards-4-adminui-sync.md](.github/plans/api-standards-4-adminui-sync.md) | Update all AdminUI API services for versioned URLs, paginated responses, and filter params |

## Key Decisions

- **Versioning strategy** — URL path segment versioning (`api/v1/`) via `Asp.Versioning.Mvc`. URL-based versioning is the most explicit and Swagger-friendly approach.
- **Metrics endpoints move to `api/v1/metrics/`** — All API endpoints including `/prometheus` and `/grafana` move under `api/v1/` for consistency. They become `api/v1/metrics/prometheus` and `api/v1/metrics/grafana`. If the format or location needs to change in a future version, versioning already covers it.
- **Pagination is mandatory on all list endpoints** — Default `page=1, pageSize=20`, max `pageSize=100`. Applies to CRUD `GetAll` endpoints and Statistics list endpoints. Time-series endpoints (`usage-timeseries`, `client-usage-breakdown`, `historical-usage`) are exempt because they are already bounded by `from`/`to` time range parameters.
- **Pagination applied in-memory** — The current data access layer loads full collections via `GetAllAsync()`. Pagination will be applied via LINQ `.Skip()/.Take()` on the returned collections. This is acceptable given the current architecture (in-memory/JSON stores). If the DAL moves to a real database later, pagination can be pushed down.
- **Filtering via query params only** — Audit confirms no existing endpoints encode filters in path segments. The plan adds optional filter parameters (e.g., `?isEnabled=true`, `?name=searchTerm`) to CRUD list endpoints for forward consistency.
- **Sub-resource endpoints are paginated** — Endpoints like `GET api/v1/clients/{id}/services` return a list of sub-resources. Even though they're nested under a parent, the number of services or pools a client has access to can grow unboundedly (e.g., a client granted access to hundreds of services). The dict responses are converted to list-of-entry responses to support standard pagination.
