# Plan: Search, Query Layer & Lucene Store — Step 1: Query Abstraction

> **Status**: ✅ Completed
> **Prerequisite**: [multi-provider-storage-4-validation-config.md](../realized/multi-provider-storage-4-validation-config.md) (the multi-provider plan must be complete)
> **Next**: [search-query-layer-2-lucene-store.md](search-query-layer-2-lucene-store.md)
> **Parent**: [search-query-layer-overview.md](search-query-layer-overview.md)

## TL;DR

Define the `DocumentQuery` model (composable field filters, text search, sort, pagination) and add `SearchAsync<T>` + `CountAsync<T>` to `IDocumentStore`. Provide a default in-memory implementation so all existing stores immediately support search via fallback.

## Reference Pattern

In [ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs](ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs):
- Existing interface with `GetAsync`, `GetAllAsync`, `SetAsync`, `DeleteAsync`, and counter methods
- New `SearchAsync` and `CountAsync` methods extend this interface

In [ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs):
- `GetAllAsync` loads from in-memory cache — the fallback will reuse this pattern

## Steps

### 1. Create `FilterOperator` enum

**File: `ClientManager.DataAccess/Stores/Models/FilterOperator.cs`**

```csharp
public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}
```

### 2. Create `FilterClause` record

**File: `ClientManager.DataAccess/Stores/Models/FilterClause.cs`**

```csharp
public record FilterClause(
    string FieldName,
    FilterOperator Operator,
    object Value);
```

`FieldName` matches the JSON property name (case-insensitive). `Value` is the comparison target (string, int, bool, DateTime, enum). Stores translate this to their native query language.

### 3. Create `SortDirection` enum

**File: `ClientManager.DataAccess/Stores/Models/SortDirection.cs`**

```csharp
public enum SortDirection
{
    Ascending,
    Descending
}
```

### 4. Create `SortClause` record

**File: `ClientManager.DataAccess/Stores/Models/SortClause.cs`**

```csharp
public record SortClause(string FieldName, SortDirection Direction);
```

### 5. Create `DocumentQuery` class

**File: `ClientManager.DataAccess/Stores/Models/DocumentQuery.cs`**

Properties:
- `Filters` (`IReadOnlyList<FilterClause>`) — all filters are AND'd together
- `TextSearch` (`string?`) — optional free-text search across all string fields
- `Sort` (`SortClause?`) — optional sort
- `Skip` (`int?`) — for pagination
- `Take` (`int?`) — for pagination / limiting results

Use a builder-style API with fluent methods:
- `Where(string field, FilterOperator op, object value)` — adds a filter clause
- `WithTextSearch(string text)` — sets the text search term
- `OrderBy(string field, SortDirection direction)` — sets sort
- `WithPagination(int skip, int take)` — sets pagination

Also provide a static `DocumentQuery.All` property that returns an empty query (no filters, no pagination) for convenience.

### 6. Create `SearchResult<T>` record

**File: `ClientManager.DataAccess/Stores/Models/SearchResult.cs`**

```csharp
public record SearchResult<T>(
    IReadOnlyList<T> Items,
    long TotalCount);
```

`TotalCount` is the total matching documents (before pagination), useful for UI pagination controls.

### 7. Add `SearchAsync` and `CountAsync` to `IDocumentStore`

**File: `ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs`**

Add these two methods to the interface:

```csharp
Task<SearchResult<T>> SearchAsync<T>(
    string collection,
    DocumentQuery query,
    CancellationToken cancellationToken = default) where T : class;

Task<long> CountAsync<T>(
    string collection,
    DocumentQuery query,
    CancellationToken cancellationToken = default) where T : class;
```

Add XML documentation explaining that stores may implement native query translation or fall back to in-memory filtering.

### 8. Create `DocumentQueryEvaluator` — the in-memory fallback engine

**File: `ClientManager.DataAccess/Stores/InMemoryQueryEvaluator.cs`**

A static helper class that takes an `IReadOnlyList<T>` and a `DocumentQuery`, and applies filters/sort/pagination in memory using reflection:

- For each `FilterClause`, get the property by name (case-insensitive), compare using the operator
- For `TextSearch`, check if any string property contains the search text (case-insensitive)
- Apply sort using `FieldName` property reflection
- Apply `Skip` and `Take`
- Return `SearchResult<T>` with filtered items and total count (before pagination)

This class is used by stores that don't have native query support, and as a fallback for any store.

### 9. Implement default `SearchAsync` and `CountAsync` on all three existing stores

**Files:**
- `ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs`
- `ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs`
- `ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs`

For now, all three use the in-memory fallback:
```csharp
public async Task<SearchResult<T>> SearchAsync<T>(
    string collection, DocumentQuery query,
    CancellationToken cancellationToken = default) where T : class
{
    var all = await GetAllAsync<T>(collection, cancellationToken);
    return InMemoryQueryEvaluator.Apply(all, query);
}

public async Task<long> CountAsync<T>(
    string collection, DocumentQuery query,
    CancellationToken cancellationToken = default) where T : class
{
    var result = await SearchAsync<T>(collection, query, cancellationToken);
    return result.TotalCount;
}
```

Native implementations for MongoDB will be added in sub-plan 3.
Native implementations for Redis (via RediSearch) will be added in sub-plan 3.
JsonFile keeps this in-memory implementation permanently — it's inherently in-memory and this is documented explicitly.

## Verification

- Project compiles without errors
- `DocumentQuery` can be constructed fluently: `new DocumentQuery().Where("Name", FilterOperator.Contains, "test").WithPagination(0, 10)`
- `InMemoryQueryEvaluator.Apply` correctly filters a test list by field value, text search, and pagination
- All three existing store implementations compile with the new interface methods
- **UI: Not directly affected — no endpoints use SearchAsync yet**
