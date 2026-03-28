# Plan: Search, Query Layer & Lucene Store — Step 5: Controller Search Parameters

> **Status**: ✅ Completed
> **Prerequisite**: [search-query-layer-4-database-migration.md](search-query-layer-4-database-migration.md)
> **Next**: [search-query-layer-6-ui-search.md](search-query-layer-6-ui-search.md)
> **Parent**: [search-query-layer-overview.md](search-query-layer-overview.md)

## TL;DR

Push the filtering that currently happens in controllers (GetAll + LINQ) down to the database layer by adding search parameters to the database interfaces and using `SearchAsync` in their implementations. Update controllers to pass filter parameters to the database instead of filtering in memory.

## Reference Pattern

In [ClientManager.Api/Controllers/ClientConfigurationsController.cs](ClientManager.Api/Controllers/ClientConfigurationsController.cs):
- `GetAll` accepts `isEnabled` and `name` query params, calls `_database.GetAllAsync()`, then filters with LINQ: `.Where(c => c.Name.Contains(name))` — this is the pattern to eliminate

In [ClientManager.Api/Controllers/ServicesController.cs](ClientManager.Api/Controllers/ServicesController.cs):
- Same pattern: `GetAll` with `isEnabled` and `name` params, `GetAllAsync()` + LINQ

In [ClientManager.Api/Controllers/ResourcePoolsController.cs](ClientManager.Api/Controllers/ResourcePoolsController.cs):
- Same pattern: `GetAll` with `name` param

## Steps

### 1. Add `SearchAsync` method to `IClientConfigurationDatabase`

**File: `ClientManager.DataAccess/Databases/Interfaces/IClientConfigurationDatabase.cs`**

Add:
```csharp
Task<SearchResult<ClientConfiguration>> SearchAsync(
    DocumentQuery query,
    CancellationToken cancellationToken = default);
```

### 2. Implement `SearchAsync` in `ClientConfigurationDatabase`

**File: `ClientManager.DataAccess/Databases/Implementations/ClientConfigurationDatabase.cs`**

Delegate to `_store.SearchAsync<ClientConfiguration>(Collection, query, cancellationToken)`.

### 3. Add `SearchAsync` method to `IEntityRepository<T>`

**File: `ClientManager.DataAccess/Repositories/Interfaces/IEntityRepository.cs`**

Add:
```csharp
Task<SearchResult<T>> SearchAsync(
    DocumentQuery query,
    CancellationToken cancellationToken = default);
```

### 4. Implement `SearchAsync` in `EntityRepository<T>`

**File: `ClientManager.DataAccess/Repositories/Implementations/EntityRepository.cs`**

Delegate to `_store.SearchAsync<T>(_collection, query, cancellationToken)`.

### 5. Update `ClientConfigurationsController.GetAll`

**File: `ClientManager.Api/Controllers/ClientConfigurationsController.cs`**

Replace the current implementation:

```csharp
// Before: GetAllAsync + LINQ filter
var configs = await _database.GetAllAsync(cancellationToken);
IReadOnlyList<ClientConfiguration> filtered = configs
    .Where(c => !isEnabled.HasValue || c.IsEnabled == isEnabled.Value)
    .Where(c => name is null || c.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
    .ToList();
return Ok(filtered.ToPagedResponse(paging));
```

With:
```csharp
var query = new DocumentQuery();
if (isEnabled.HasValue)
    query = query.Where("IsEnabled", FilterOperator.Equals, isEnabled.Value);
if (name is not null)
    query = query.Where("Name", FilterOperator.Contains, name);
query = query.WithPagination(paging.Skip, paging.PageSize);

var result = await _database.SearchAsync(query, cancellationToken);
return Ok(new PagedResponse<ClientConfiguration>(result.Items, result.TotalCount, paging));
```

Note: The `PagedResponse` constructor may need a new overload that accepts `items`, `totalCount`, and `paging` directly — check the existing `ToPagedResponse` extension and adapt.

### 6. Update `ServicesController.GetAll`

**File: `ClientManager.Api/Controllers/ServicesController.cs`**

Same pattern: build a `DocumentQuery` from `isEnabled` and `name` params, call `_repository.SearchAsync(query)`, return the result as a `PagedResponse`.

### 7. Update `ResourcePoolsController.GetAll`

**File: `ClientManager.Api/Controllers/ResourcePoolsController.cs`**

Same pattern: build a `DocumentQuery` from `name` param, call `_repository.SearchAsync(query)`.

### 8. Add a general-purpose `search` query parameter

**File: All three controllers above**

Add an optional `[FromQuery] string? search` parameter to each `GetAll` action that maps to `DocumentQuery.TextSearch`. This enables free-text search across all string fields — searching for "acme" could match client name, ID, or any indexed string.

Update Swagger documentation to describe the parameter.

### 9. Update `PagedResponse` to support `SearchResult`

**File: `ClientManager.Shared/Models/Responses/PagedResponse.cs`** (or the pagination extensions file)

Ensure `PagedResponse` can be constructed from a `SearchResult<T>` that already contains `TotalCount`. The current `ToPagedResponse` extension calculates total from the list length, which is wrong when the search already paginated server-side. Add:

```csharp
public static PagedResponse<T> ToPagedResponse<T>(
    this SearchResult<T> result, PagedRequest paging)
{
    // result.Items is already paginated, result.TotalCount is the global total
    return new PagedResponse<T>(result.Items, result.TotalCount, paging.Page, paging.PageSize);
}
```

Adjust the `PagedResponse` record to accept a pre-computed total count.

## Verification

- Project compiles without errors
- `GET /api/v1/clients?name=test` returns only clients whose name contains "test" — verified via Swagger
- `GET /api/v1/clients?isEnabled=true&name=acme` returns only enabled clients with "acme" in the name
- `GET /api/v1/services?search=email` returns services matching the free-text search
- `GET /api/v1/resource-pools?name=gpu` returns pools matching the name filter
- Pagination works correctly: `page=2&pageSize=5` returns the second page with correct `TotalCount`
- Empty filters return all items (backward compatible)
- **UI: Navigate to the Clients page — verify the client list still loads**
- **UI: Navigate to the Services page — verify the service list still loads**
- **UI: Navigate to the Resource Pools page — verify the pool list still loads**
- **UI: Verify pagination controls on all list pages show the correct total counts**
