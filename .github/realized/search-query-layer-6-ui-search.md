# Plan: Search, Query Layer & Lucene Store — Step 6: UI Search Integration

> **Status**: ✅ Completed
> **Prerequisite**: [search-query-layer-5-controller-search.md](search-query-layer-5-controller-search.md)
> **Next**: None — this is the final step.
> **Parent**: [search-query-layer-overview.md](search-query-layer-overview.md)

## TL;DR

Update admin UI API services to use the new POST search endpoints (`POST /search` with `DocumentQuery` body, `SearchResult<T>` response). Add filter UI to list pages so users can filter directly instead of scrolling. The query models live in `ClientManager.Shared.Models.Search` and are shared between API and UI.

## Important Context (Post-Redesign)

The API endpoints were redesigned after the original plan was written:
- **List endpoints are now `POST /search`** — not `GET` with query string params
- **Request body is a `DocumentQuery`** — filters, sort, skip, take in one object
- **Response is `SearchResult<T>`** — contains `Items` (IEnumerable) and `TotalCount` (long)
- **No free-text search** — only field-level `FilterClause` filters are supported
- **Statistics list endpoints** also use POST search: `POST /statistics/clients/search`, `POST /statistics/services/search`, `POST /statistics/resource-pools/search` — response is `SearchResult<ProjectedType>`

## Reference Pattern

In [ClientManager.AdminUI/Services/ClientApiService.cs](ClientManager.AdminUI/Services/ClientApiService.cs):
- API service classes that make HTTP calls to the API controllers
- These need to switch from `GET` with query strings to `POST` with `DocumentQuery` body

In [ClientManager.Shared/Models/Search/](ClientManager.Shared/Models/Search/):
- `DocumentQuery`, `FilterClause`, `FilterOperator`, `SearchResult<T>`, `SortClause`, `SortDirection`
- Already in the Shared project — the AdminUI can use them directly

## Steps

### 1. Update `ClientApiService` to use POST search

**File: `ClientManager.AdminUI/Services/ClientApiService.cs`**

Change the list method to:
- Accept an optional `DocumentQuery?` parameter
- POST to `/api/v1/clients/search` with the query as JSON body
- Deserialize `SearchResult<ClientConfiguration>` instead of `PagedResponse<ClientConfiguration>`

### 2. Update `ServiceApiService` to use POST search

**File: `ClientManager.AdminUI/Services/ServiceApiService.cs`**

Same pattern: POST to `/api/v1/services/search` with `DocumentQuery` body, return `SearchResult<Service>`.

### 3. Update `ResourcePoolApiService` to use POST search

**File: `ClientManager.AdminUI/Services/ResourcePoolApiService.cs`**

Same pattern: POST to `/api/v1/resource-pools/search`, return `SearchResult<ResourcePool>`.

### 4. Update `GlobalRateLimitApiService` to use POST search

**File: `ClientManager.AdminUI/Services/GlobalRateLimitApiService.cs`**

Same pattern: POST to `/api/v1/global-rate-limits/search`, return `SearchResult<GlobalRateLimit>`.

### 5. Update statistics API calls to use POST search

Update any admin UI service that calls the statistics list endpoints:
- `POST /api/v1/statistics/clients/search` → `SearchResult<ClientSummaryResponse>`
- `POST /api/v1/statistics/services/search` → `SearchResult<ServiceStatisticsResponse>`
- `POST /api/v1/statistics/resource-pools/search` → `SearchResult<ResourcePoolStatisticsResponse>`

### 6. Add a shared `SearchBar` component

**File: `ClientManager.AdminUI/Components/Shared/SearchBar.razor`**

A reusable component with:
- A text input for name filtering (debounced, e.g. 300ms delay)
- An `EventCallback<string>` for `OnSearchChanged`
- Optional filter slots via `RenderFragment` for page-specific filters (like an "Enabled" toggle)

On change, the parent page constructs a `DocumentQuery` with the appropriate `FilterClause` entries and re-fetches.

### 7. Integrate search into the Clients list page

**File: The Blazor page that lists clients** (find the exact file in `Components/Pages/`)

- Add `SearchBar` component at the top of the list
- On name input change, build a `DocumentQuery` with `Where("Name", Contains, value)` and re-fetch
- Add an "Enabled" toggle filter that adds `Where("IsEnabled", Equals, true/false)`
- Show a "No results" message when the search returns empty results
- Update pagination to work with `SearchResult<T>.TotalCount` and `DocumentQuery.Skip/Take`

### 8. Integrate search into the Services list page

Same pattern as step 7 but for Services. Add "Enabled" toggle filter.

### 9. Integrate search into the Resource Pools list page

Same pattern. Name search only (no enabled filter for pools unless one exists).

### 10. Integrate search into the Global Rate Limits list page

Same pattern. Add a target type dropdown filter (`Service` / `ResourcePool` / All) using `Where("TargetType", Equals, "Service")`.

## Verification

- Project compiles without errors
- **UI: Navigate to the Clients page — a search bar is visible at the top of the list**
- **UI: Type a client name into the search bar — the list filters to matching clients after a short debounce**
- **UI: Toggle the "Enabled" filter — the list shows only enabled/disabled clients**
- **UI: Clear the search — all clients reappear**
- **UI: Navigate to the Services page — search bar works, filters by name and enabled state**
- **UI: Navigate to the Resource Pools page — search bar works, filters by name**
- **UI: Navigate to the Global Rate Limits page — search bar works, target type dropdown filters correctly**
- **UI: Verify that pagination still works correctly alongside search (total counts update)**
- **UI: Verify that empty search results show a "No results found" message, not an empty table**
- **UI: Take screenshots of each page to confirm layout consistency**
