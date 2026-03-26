# Plan: Search, Query Layer & Lucene Store — Step 4: Database Layer Migration

> **Status**: 🔲 Not started
> **Prerequisite**: [search-query-layer-3-existing-store-search.md](search-query-layer-3-existing-store-search.md)
> **Next**: [search-query-layer-5-controller-search.md](search-query-layer-5-controller-search.md)
> **Parent**: [search-query-layer-overview.md](search-query-layer-overview.md)

## TL;DR

Migrate all `*Database` implementations that currently use `GetAllAsync` + LINQ to use `SearchAsync` with appropriate `DocumentQuery` filters. This pushes filtering down to the storage engine, eliminating full-collection scans on the hot path.

## Reference Pattern

In [ClientManager.DataAccess/Databases/Implementations/GlobalRateLimitDatabase.cs](ClientManager.DataAccess/Databases/Implementations/GlobalRateLimitDatabase.cs):
- `GetByTargetAsync` calls `_store.GetAllAsync<GlobalRateLimit>(Collection)` then uses `.FirstOrDefault(g => g.TargetId == targetId && g.TargetType == targetType)` — this is the pattern to replace

In [ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs](ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs):
- `GetByTargetAsync` calls `GetAllAsync` then filters by `TargetId`, `TargetType`, `Granularity`
- `GetAllByGranularityAsync` calls `GetAllAsync` then filters by `Granularity`

In [ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs](ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs):
- `GetActiveCountsByPoolAsync` calls `GetAllAsync` then filters and groups
- `GetActiveCountsByPoolAndClientAsync` same pattern
- `CleanupExpiredAsync` calls `GetAllAsync` then iterates — this one is acceptable since it needs to modify each match

## Steps

### 1. Migrate `GlobalRateLimitDatabase.GetByTargetAsync`

**File: `ClientManager.DataAccess/Databases/Implementations/GlobalRateLimitDatabase.cs`**

Replace the `GetAllAsync` + LINQ with:
```csharp
var query = new DocumentQuery()
    .Where("TargetId", FilterOperator.Equals, targetId)
    .Where("TargetType", FilterOperator.Equals, targetType.ToString())
    .WithPagination(0, 1);

var result = await _store.SearchAsync<GlobalRateLimit>(Collection, query, cancellationToken);
return result.Items.FirstOrDefault();
```

### 2. Migrate `GlobalRateLimitDatabase.GetByTargetTypeAsync`

Same file. Replace with:
```csharp
var query = new DocumentQuery()
    .Where("TargetType", FilterOperator.Equals, targetType.ToString());

var result = await _store.SearchAsync<GlobalRateLimit>(Collection, query, cancellationToken);
return result.Items;
```

### 3. Migrate `UsageSnapshotDatabase.GetByTargetAsync`

**File: `ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs`**

Replace with:
```csharp
var query = new DocumentQuery()
    .Where("TargetId", FilterOperator.Equals, targetId)
    .Where("TargetType", FilterOperator.Equals, targetType.ToString())
    .Where("Granularity", FilterOperator.Equals, granularity.ToString());

var result = await _store.SearchAsync<UsageSnapshot>(Collection, query, cancellationToken);
return result.Items;
```

### 4. Migrate `UsageSnapshotDatabase.GetAllByGranularityAsync`

Same file. Replace with:
```csharp
var query = new DocumentQuery()
    .Where("Granularity", FilterOperator.Equals, granularity.ToString());

var result = await _store.SearchAsync<UsageSnapshot>(Collection, query, cancellationToken);
return result.Items;
```

### 5. Migrate `ResourceAllocationDatabase.GetActiveCountsByPoolAsync`

**File: `ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs`**

This is more complex because it needs grouping, which `SearchAsync` doesn't support. Two options:

**Option A (pragmatic)**: Use `SearchAsync` with a filter for active allocations (`IsReleased == false, ExpiresAt > now`), then group in memory. This still avoids loading released/expired allocations:
```csharp
var query = new DocumentQuery()
    .Where("IsReleased", FilterOperator.Equals, false)
    .Where("ExpiresAt", FilterOperator.GreaterThan, DateTime.UtcNow);

var result = await _store.SearchAsync<ResourceAllocation>(Collection, query, cancellationToken);
return result.Items
    .GroupBy(a => a.ResourcePoolId)
    .ToDictionary(g => g.Key, g => g.Count());
```

**Option B**: Add a `GroupCountAsync` method to `IDocumentStore` — YAGNI for now. Go with Option A.

### 6. Migrate `ResourceAllocationDatabase.GetActiveCountsByPoolAndClientAsync`

Same file, same approach as step 5 but group by `(ResourcePoolId, ClientId)`.

### 7. Leave `CleanupExpiredAsync` and `ReconcileCountersAsync` as-is

These methods need to iterate, modify, and rewrite documents — they legitimately need all matching documents. However, `CleanupExpiredAsync` can benefit from filtering to only non-released allocations:

```csharp
var query = new DocumentQuery()
    .Where("IsReleased", FilterOperator.Equals, false);
var result = await _store.SearchAsync<ResourceAllocation>(Collection, query, cancellationToken);
// Then filter by ExpiresAt <= now and process
```

This avoids loading already-released allocations into memory.

## Verification

- Project compiles without errors
- `GlobalRateLimitDatabase.GetByTargetAsync` returns the correct rate limit without loading all documents (verify via debug logging or breakpoint)
- `UsageSnapshotDatabase.GetByTargetAsync` returns filtered snapshots
- `ResourceAllocationDatabase.GetActiveCountsByPoolAsync` returns correct counts, excluding released/expired allocations from the initial query
- All existing endpoints return the same data as before the migration
- **UI: Navigate to the dashboard overview — verify system stats are unchanged**
- **UI: Open the Global Rate Limits page — verify all rate limits display correctly**
- **UI: Open the Resource Pools page — verify active allocation counts are correct**
- **UI: Open the time-series charts — verify usage data renders without changes**
