# Plan: API Standards — Step 3: Apply Pagination & Query Filtering to Controllers

> **Status**: ✅ Completed
> **Prerequisite**: [api-standards-2-pagination-foundation.md](api-standards-2-pagination-foundation.md)
> **Next**: [api-standards-4-adminui-sync.md](api-standards-4-adminui-sync.md)
> **Parent**: [api-standards-overview.md](api-standards-overview.md)

## TL;DR

Apply `PagedRequest`/`PagedResponse<T>` and optional query param filters to every collection-returning endpoint across all controllers. After this step, no API endpoint returns an unbounded list. Time-series endpoints are exempt (already bounded by `from`/`to`). Sub-resource dictionaries on `ClientConfigurationsController` are converted to paginated list-of-entry responses.

## Reference Pattern

In [ClientManager.Api/Controllers/GlobalRateLimitsController.cs](ClientManager.Api/Controllers/GlobalRateLimitsController.cs):
- `GetAll` already accepts an optional `[FromQuery] TargetType? targetType` filter parameter
- This is the pattern to follow: optional `[FromQuery]` filter params alongside the `[FromQuery] PagedRequest`

In [ClientManager.Api/Extensions/PaginationExtensions.cs](ClientManager.Api/Extensions/PaginationExtensions.cs) (created in Step 2):
- `source.ToPagedResponse(paging)` is the standard call to paginate any `IReadOnlyList<T>`

## Steps

### 1. Update `ClientConfigurationsController.GetAll`

Add `[FromQuery] PagedRequest paging` plus optional filters `[FromQuery] bool? isEnabled` and `[FromQuery] string? name`. Filter the collection in-memory (LINQ `.Where()`) then return `filtered.ToPagedResponse(paging)`. Update `[ProducesResponseType]` to `PagedResponse<ClientConfiguration>`. Add necessary `using` statements for `PagedRequest`, `PagedResponse`, and the pagination extension.

### 2. Update `ResourcePoolsController.GetAll`

Same pattern as Step 1. Add `[FromQuery] PagedRequest paging` and optional `[FromQuery] string? name` filter (case-insensitive contains on `Name`). Return `PagedResponse<ResourcePool>`.

### 3. Update `ServicesController.GetAll`

Same pattern. Add paging + optional `isEnabled` and `name` filters. Return `PagedResponse<Service>`.

### 4. Update `GlobalRateLimitsController.GetAll`

This endpoint already has a `targetType` filter. Add `[FromQuery] PagedRequest paging` alongside it. Apply pagination after the existing `targetType` filtering logic. Return `PagedResponse<GlobalRateLimit>`.

### 5. Update `StatisticsController` list endpoints

Add `[FromQuery] PagedRequest paging` and apply `.ToPagedResponse(paging)` to the result lists in these four endpoints:

| Endpoint | Return type after change |
|---|---|
| `GetClients` (GET statistics/clients) | `PagedResponse<ClientSummaryResponse>` |
| `GetServices` (GET statistics/services) | `PagedResponse<ServiceStatisticsResponse>` |
| `GetResourcePools` (GET statistics/resource-pools) | `PagedResponse<ResourcePoolStatisticsResponse>` |
| `GetClientSummaries` (GET statistics/client-summaries) | `PagedResponse<ClientSummaryRow>` — paginate the `.Rows` collection from the service result |

Keep the existing business logic in each method unchanged — just wrap the final result in pagination.

### 6. Paginate sub-resource dict endpoints on `ClientConfigurationsController`

Create `ClientManager.Api/Models/Responses/KeyedEntry.cs` — a generic record `KeyedEntry<T>(string Key, T Value)` that wraps a dictionary entry for paginated responses.

Update two endpoints:
- **`GetServices`** (GET clients/{id}/services) — convert `config.Services` dictionary to `IReadOnlyList<KeyedEntry<ServiceAccessSettings>>` via LINQ `.Select()`, then apply `.ToPagedResponse(paging)`.
- **`GetResourcePools`** (GET clients/{id}/resource-pools) — same pattern with `KeyedEntry<ResourcePoolSettings>`.

Both accept `[FromQuery] PagedRequest paging` and update their `[ProducesResponseType]` accordingly.

### 7. Endpoints that remain unchanged (exempt)

The following endpoints are **not** paginated — document the rationale as code comments:

| Endpoint | Reason |
|---|---|
| `GET statistics/overview` | Returns a single aggregate object, not a collection |
| `GET statistics/global-usage` | Returns a single aggregate object |
| `GET statistics/usage-timeseries` | Bounded by `from`/`to` time range parameters |
| `GET statistics/client-usage-breakdown` | Bounded by `from`/`to` time range parameters |
| `GET statistics/historical-usage` | Bounded by `from`/`to` time range parameters |
| `GET statistics/clients/{id}` | Returns a single resource |
| `GET statistics/services/{id}` | Returns a single resource |
| `GET statistics/resource-pools/{id}` | Returns a single resource |
| `POST access/check` | Returns a single result |
| `GET access/{clientId}` | Returns a single report |
| `POST resources/acquire` | Action, not a list |
| `POST resources/release` | Action, not a list |

### 8. Update `[ProducesResponseType]` attributes

Ensure every modified endpoint's `[ProducesResponseType]` uses `PagedResponse<T>` instead of `IReadOnlyList<T>` so Swagger documents the envelope correctly. This is shown in each step's code snippet above.

### 9. Update XML doc comments

Ensure the `<returns>` and `<response code="200">` tags reflect "paginated" language for all modified endpoints. Follow the pattern in Step 1's snippet.

## Verification

- Project compiles without errors (`dotnet build`).
- Swagger at `/docs` shows `PagedResponse<T>` schema for all modified list endpoints.
- `GET /api/v1/clients` returns `{ items: [...], page: 1, pageSize: 20, totalCount: N, totalPages: M }`.
- `GET /api/v1/clients?page=1&pageSize=2` returns at most 2 items with correct `totalCount`.
- `GET /api/v1/clients?isEnabled=true` returns only enabled clients, paginated.
- `GET /api/v1/services?name=foo` returns only services with "foo" in the name, paginated.
- `GET /api/v1/global-rate-limits?targetType=Service` still works, now paginated.
- `GET /api/v1/statistics/resource-pools` returns paginated pool stats.
- Time-series endpoints still return raw arrays (no pagination envelope).
- **UI: AdminUI will show broken data (expects arrays, gets envelopes)** — handled in Step 4.
- **UI: After Step 4 is complete, navigate to Clients page — verify the table populates correctly with paginated data.**
- **UI: Navigate to the Resource Pools page — verify pool list loads and displays correctly.**
