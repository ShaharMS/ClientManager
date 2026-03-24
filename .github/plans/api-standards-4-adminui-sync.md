# Plan: API Standards — Step 4: AdminUI API Service Sync

> **Status**: ✅ Completed
> **Prerequisite**: [api-standards-3-apply-pagination-filtering.md](api-standards-3-apply-pagination-filtering.md)
> **Next**: None — this is the final step.
> **Parent**: [api-standards-overview.md](api-standards-overview.md)

## TL;DR

Update all AdminUI API service classes to use the new `api/v1/` URL paths and unwrap `PagedResponse<T>` envelopes. Services that previously returned `List<T>` from `GetAll` methods now accept optional pagination/filter parameters and return the items from the paginated response. The AdminUI currently loads full lists for display in tables and dropdowns, so the default behavior must fetch enough data (high `pageSize`) or all pages as needed.

## Reference Pattern

In [ClientManager.AdminUI/Services/ClientApiService.cs](ClientManager.AdminUI/Services/ClientApiService.cs):
- Services take `IHttpClientFactory`, create a named `HttpClient`
- `GetAllAsync()` calls `_httpClient.GetFromJsonAsync<List<T>>("api/clients")`
- 30-second in-memory cache with `_cachedAll` / `_cachedAllAt` pattern

In [ClientManager.AdminUI/Services/StatisticsApiService.cs](ClientManager.AdminUI/Services/StatisticsApiService.cs):
- Uses URL string building with `Uri.EscapeDataString` for query params
- Returns DTOs defined as records at the bottom of the file

## Steps

### 1. Create a shared `PagedResponse<T>` DTO in AdminUI

Create `ClientManager.AdminUI/Models/PagedResponse.cs` — a record mirroring the API's `PagedResponse<T>` with properties: `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`.

### 2. Update `ClientApiService`

- Change all URL paths from `api/clients` to `api/v1/clients`.
- Change `GetAllAsync` to deserialize `PagedResponse<ClientConfiguration>` instead of `List<ClientConfiguration>`, return `.Items`. Use `pageSize=100` query param to approximate current "load all" behavior.
- Update `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync` paths to `api/v1/clients/...`.

### 3. Update `ServiceApiService`

Same pattern as Step 2 — update all paths from `api/services` to `api/v1/services`, unwrap `PagedResponse` in `GetAllAsync`.

### 4. Update `ResourcePoolApiService`

Same pattern — update paths from `api/resource-pools` to `api/v1/resource-pools`, unwrap `PagedResponse`.

### 5. Update `GlobalRateLimitApiService`

Update paths from `api/global-rate-limits` to `api/v1/global-rate-limits`. Both `GetAllAsync` and `GetByTargetTypeAsync` now return paginated responses — unwrap both.

### 6. Update `StatisticsApiService`

Update all URL paths from `api/statistics/...` to `api/v1/statistics/...`.

For endpoints that now return `PagedResponse<T>` instead of arrays:
- `GetResourcePoolStatsAsync` — unwrap `PagedResponse<ResourcePoolStatistics>`
- `GetClientSummariesAsync` — the API now returns `PagedResponse<ClientSummaryRow>` instead of the old `ClientSummaries` wrapper. Update deserialization accordingly.

Time-series endpoints don't change response shape — only update their URL prefix.
`GetOverviewAsync` and `GetGlobalUsageStatsAsync` return single objects — only update URL prefix.

### 7. Update record DTOs in `StatisticsApiService`

The `ResourcePoolStatistics` and `ClientSummaries` records defined at the bottom of `StatisticsApiService.cs` may need adjusting if the response shape changed. Verify the Swagger schema matches the locally defined DTOs.

## Verification

- Both `ClientManager.Api` and `ClientManager.AdminUI` compile without errors.
- **UI: Start both projects. Navigate to the Clients page — verify the table populates, shows correct data, and pagination does not cause empty results.**
- **UI: Navigate to the Services page — verify services load and display.**
- **UI: Navigate to the Resource Pools page — verify pools load with correct slot counts.**
- **UI: Open a client detail view — verify sub-resources (service settings, pool settings) display correctly.**
- **UI: Navigate to the Dashboard/Statistics page — verify overview stats, pool utilization, and charts load without errors.**
- **UI: Toggle a chart time range — verify time-series data still flows through correctly.**
- **UI: Take a screenshot on each page to confirm no layout breakage or error banners.**
- No hardcoded `api/clients` or `api/services` etc. paths remain in AdminUI (search for `"api/` without `v1`).
- Verify any Prometheus/Grafana scraping configs are updated to `api/v1/metrics/prometheus` and `api/v1/metrics/grafana` if applicable.
