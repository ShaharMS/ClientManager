# Plan: Search, Query Layer & Lucene Store — Step 3: Native Search for Existing Stores

> **Status**: ✅ Completed
> **Prerequisite**: [search-query-layer-2-lucene-store.md](search-query-layer-2-lucene-store.md)
> **Next**: [search-query-layer-4-database-migration.md](search-query-layer-4-database-migration.md)
> **Parent**: [search-query-layer-overview.md](search-query-layer-overview.md)

## TL;DR

Upgrade `MongoDBDocumentStore.SearchAsync` to native MongoDB query translation. Upgrade `RedisDocumentStore.SearchAsync` to use the RediSearch module (`FT.SEARCH`) for native server-side filtering when available, with graceful fallback to in-memory if the module isn't loaded. Document `JsonFileDocumentStore.SearchAsync` as an explicit in-memory implementation — it's inherently in-memory and there's no alternative.

## Reference Pattern

In [ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs):
- Already uses `Builders<BsonDocument>.Filter` for `_id` lookups
- Native search extends this pattern with field-level filters

In [ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs):
- Currently stores documents as JSON strings inside Redis hashes
- RediSearch `FT.CREATE` + `FT.SEARCH` provides native full-text and field-level search on hash or JSON data

## Steps

### 1. Implement native `SearchAsync` for MongoDB

**File: `ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs`**

Replace the in-memory fallback `SearchAsync` with a native MongoDB implementation:

1. **Build a `FilterDefinition<BsonDocument>`** from the `DocumentQuery.Filters`:
   - `Equals` → `Builders<BsonDocument>.Filter.Eq(fieldName, value)`
   - `NotEquals` → `Filter.Ne`
   - `Contains` → `Filter.Regex(fieldName, new BsonRegularExpression(Regex.Escape(value), "i"))`
   - `StartsWith` → `Filter.Regex(fieldName, new BsonRegularExpression("^" + Regex.Escape(value), "i"))`
   - `GreaterThan` → `Filter.Gt`
   - `GreaterThanOrEqual` → `Filter.Gte`
   - `LessThan` → `Filter.Lt`
   - `LessThanOrEqual` → `Filter.Lte`
   - Combine all with `Filter.And`

2. **Handle `TextSearch`**: Use an `$or` filter with `Regex` across discoverable string fields. The document sizes in this system are small enough that this is efficient without text indexes.

3. **Apply sort**: If `query.Sort` is set, use `Builders<BsonDocument>.Sort.Ascending/Descending`.

4. **Apply pagination**: Use `.Skip(query.Skip).Limit(query.Take)`.

5. **Get total count**: Use `CountDocumentsAsync` with the same filter (before skip/limit).

6. **Deserialize**: Use the existing `DeserializeDocument<T>` helper.

### 2. Implement native `CountAsync` for MongoDB

**File: `ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs`**

Build the same filter as `SearchAsync` but only call `CountDocumentsAsync` — no document retrieval.

### 3. Add NRedisStack package for RediSearch support

**File: `ClientManager.DataAccess/ClientManager.DataAccess.csproj`**

Add the `NRedisStack` NuGet package alongside the existing `StackExchange.Redis`. NRedisStack is the official Redis client library for .NET that provides typed APIs for Redis modules including RediSearch (`FT.*` commands).

### 4. Implement RediSearch-based `SearchAsync` for Redis

**File: `ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs`**

RediSearch operates on Redis hashes or JSON documents. The current store uses hashes where each collection is a single hash key with document IDs as field names and JSON as values. RediSearch requires a different storage layout:

**Storage layout change**: When search is used, documents must be stored as **individual Redis hash keys** (e.g., `collection:{collectionName}:{id}`) with each JSON property as a separate hash field, rather than as a single hash with JSON blob values. This is required for RediSearch to index individual fields.

However, this is a significant structural change. Instead, take this approach:

**Approach: JSON document type with RediSearch**

1. **On startup / first use of a collection**, check if the RediSearch module is available by executing `MODULE LIST` and looking for the `search` module.

2. **If RediSearch is available**:
   - Store documents using `JSON.SET` (RedisJSON) at keys like `doc:{collection}:{id}` as full JSON documents
   - Create a RediSearch index per collection using `FT.CREATE` with `ON JSON` and schema fields derived from the document type
   - For `SearchAsync`, translate `DocumentQuery` filters to a RediSearch query string:
     - `Equals` on string → `@FieldName:{value}` (tag field) or `@FieldName:value` (text field)
     - `Contains` → `@FieldName:*value*` (text search with wildcards)
     - `Numeric GreaterThan/LessThan` → `@FieldName:[min max]`
     - `TextSearch` → free-text search across all TEXT fields
   - Execute `FT.SEARCH` with `LIMIT offset count` for pagination
   - Parse results back into `SearchResult<T>`

3. **If RediSearch is NOT available**:
   - Fall back to the existing hash-based storage + `InMemoryQueryEvaluator`
   - Log a warning at startup: "RediSearch module not detected — search operations will use in-memory filtering. For native search support, enable the RediSearch module on your Redis server."

**Key implementation details:**

- **Index creation**: Lazy-create the index on first `SearchAsync` call for a collection. Use `FT.CREATE ... IF NOT EXISTS` (Redis 7.2+) or catch "Index already exists" errors.
- **Schema detection**: Use a convention — index common field patterns: string fields as `TAG` + `TEXT`, numeric fields as `NUMERIC`, boolean fields as `TAG`. The implementer can inspect the first document or use a registration-based approach.
- **`CountAsync`**: Use `FT.SEARCH` with `LIMIT 0 0` — returns total count without documents.

### 5. Update `RedisDocumentStore` storage to support both layouts

**File: `ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs`**

Since RediSearch with JSON mode requires `JSON.SET`/`JSON.GET` instead of hash operations, and we need the existing `GetAsync`/`SetAsync` to work with both:

- Add a `_useRedisJsonStorage` boolean flag set during initialization based on module detection
- If RedisJSON + RediSearch are available: `SetAsync` uses `JSON.SET`, `GetAsync` uses `JSON.GET`, `GetAllAsync` uses `FT.SEARCH *` with no filter
- If not available: keep the current hash-based implementation unchanged
- `DeleteAsync`: Delete the JSON key and the document is automatically removed from the search index

### 6. Document `JsonFileDocumentStore.SearchAsync` as explicit in-memory

**File: `ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs`**

The `SearchAsync` implementation uses `InMemoryQueryEvaluator.Apply()`. Add a clear XML doc comment explaining:

```csharp
/// <summary>
/// Executes a search query against the in-memory collection cache.
/// <para>
///     The JSON file store maintains a full in-memory cache of all documents (loaded on first
///     access and kept in sync via write-through). All filtering, sorting, and pagination are
///     applied in memory using <see cref="InMemoryQueryEvaluator"/>. This is functionally
///     correct but does not scale — for production workloads with large collections, use the
///     Lucene, MongoDB, or Redis (with RediSearch) providers which support native server-side
///     query execution.
/// </para>
/// </summary>
```

Same treatment for `CountAsync`.

## Verification

- Project compiles without errors
- **MongoDB**: `SearchAsync` with `FilterClause(FieldName: "Name", Operator: Contains, Value: "test")` returns correct results against a running MongoDB instance
- **MongoDB**: `SearchAsync` with pagination (`Skip: 5, Take: 10`) returns a subset and the correct `TotalCount`
- **MongoDB**: `CountAsync` returns the total without loading documents
- **Redis (with RediSearch)**: If a Redis instance with the Search module is available, `SearchAsync` executes `FT.SEARCH` and returns correct results
- **Redis (without RediSearch)**: Falls back gracefully to in-memory filtering with a logged warning
- **Redis**: `CountAsync` returns correct counts
- **JsonFile**: `SearchAsync` works correctly via in-memory — XML docs clearly document the limitation
- **UI: Not directly affected yet — no endpoints use SearchAsync at this point**
