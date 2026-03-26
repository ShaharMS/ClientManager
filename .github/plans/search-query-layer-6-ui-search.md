# Plan: Search, Query Layer & Lucene Store — Step 6: UI Search Integration

> **Status**: 🔲 Not started
> **Prerequisite**: [search-query-layer-5-controller-search.md](search-query-layer-5-controller-search.md)
> **Next**: None — this is the final step.
> **Parent**: [search-query-layer-overview.md](search-query-layer-overview.md)

## TL;DR

Add search/filter fields to the admin UI list pages (Clients, Services, Resource Pools, Global Rate Limits) and wire them to the new API search parameters. Users can now filter and search directly in the UI instead of scrolling through unfiltered lists.

## Reference Pattern

In [ClientManager.AdminUI/Services/ClientApiService.cs](ClientManager.AdminUI/Services/ClientApiService.cs):
- API service classes that make HTTP calls to the API controllers
- These need new parameters for search/filter values

In [ClientManager.AdminUI/Components/Pages/](ClientManager.AdminUI/Components/Pages/):
- Existing Blazor pages for Clients, Services, Resource Pools
- These need search input fields and filter dropdowns that feed into the API service calls

## Steps

### 1. Update `ClientApiService` to accept search parameters

**File: `ClientManager.AdminUI/Services/ClientApiService.cs`**

Add optional `string? search`, `string? name`, `bool? isEnabled` parameters to the list/GetAll method. Build query string parameters and append to the API URL.

### 2. Update `ServiceApiService` to accept search parameters

**File: `ClientManager.AdminUI/Services/ServiceApiService.cs`**

Same pattern: add `string? search`, `string? name`, `bool? isEnabled` parameters.

### 3. Update `ResourcePoolApiService` to accept search parameters

**File: `ClientManager.AdminUI/Services/ResourcePoolApiService.cs`**

Same pattern: add `string? search`, `string? name` parameters.

### 4. Update `GlobalRateLimitApiService` to accept search parameters

**File: `ClientManager.AdminUI/Services/GlobalRateLimitApiService.cs`**

Add `string? search`, `TargetType? targetType` parameters. The target type filter already exists in the controller; this wires it to the UI.

### 5. Add a shared `SearchBar` component

**File: `ClientManager.AdminUI/Components/Shared/SearchBar.razor`**

A reusable component with:
- A text input for free-text search (debounced, e.g. 300ms delay)
- An `EventCallback<string>` for `OnSearchChanged`
- Optional filter slots via `RenderFragment` for page-specific filters (like an "Enabled" toggle)

Keep it simple — a single input with a search icon, styled to match the existing UI.

### 6. Integrate search into the Clients list page

**File: The Blazor page that lists clients** (find the exact file in `Components/Pages/`)

- Add `SearchBar` component at the top of the list
- On search change, call `ClientApiService` with the search term
- Add an "Enabled" toggle filter that maps to `isEnabled` parameter
- Re-fetch the list when search or filter changes
- Show a "No results" message when the search returns empty

### 7. Integrate search into the Services list page

Same pattern as step 6 but for Services. Add "Enabled" toggle filter.

### 8. Integrate search into the Resource Pools list page

Same pattern. Name search only (no enabled filter for pools unless one exists).

### 9. Integrate search into the Global Rate Limits list page

Same pattern. Add a target type dropdown filter (`Service` / `ResourcePool` / All).

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
