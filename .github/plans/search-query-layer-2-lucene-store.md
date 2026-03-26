# Plan: Search, Query Layer & Lucene Store — Step 2: Lucene.NET Store Implementation

> **Status**: 🔲 Not started
> **Prerequisite**: [search-query-layer-1-query-abstraction.md](search-query-layer-1-query-abstraction.md)
> **Next**: [search-query-layer-3-existing-store-search.md](search-query-layer-3-existing-store-search.md)
> **Parent**: [search-query-layer-overview.md](search-query-layer-overview.md)

## TL;DR

Implement `LuceneDocumentStore : IDocumentStore` — a file-based, PVC-friendly store that uses Lucene.NET for document storage and native search. Add `LuceneStoreOptions`, extend the `PersistenceProvider` enum with `Lucene`, and register it as a new provider option in the DI wiring.

## Reference Pattern

In [ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs):
- File-based storage with a data directory path
- In-memory cache for reads
- Write-through pattern
- Lucene will follow a similar directory-based approach but use an index instead of raw JSON files

In [ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs):
- Implements the full `IDocumentStore` interface including counters
- Serializes documents to/from JSON
- Lucene will store the full JSON as a stored field and index queryable fields separately

## Steps

### 1. Add Lucene.NET NuGet packages

**File: `ClientManager.DataAccess/ClientManager.DataAccess.csproj`**

Add:
- `Lucene.Net` (version `4.8.0-beta00016` — latest stable 4.8 beta, which is the most mature .NET port)
- `Lucene.Net.Analysis.Common` — for standard analyzers
- `Lucene.Net.QueryParser` — for parsing text search queries

### 2. Add `Lucene` to `PersistenceProvider` enum

**File: `ClientManager.Shared/Models/Enums/PersistenceProvider.cs`**

Add a new member:
```csharp
/// <summary>
/// A Lucene.NET embedded search index, intended for PVC-based deployments
/// that need full-text and field-level search without an external database.
/// </summary>
Lucene
```

### 3. Create `LuceneStoreOptions` class

**File: `ClientManager.Api/Models/Configuration/LuceneStoreOptions.cs`**

Properties:
- `IndexDirectory` (string, default `"./lucene-index"`) — path to the Lucene index files
- `CommitIntervalSeconds` (int, default `1`) — how often to commit pending writes to disk (batching commits improves write throughput)
- `MaxBufferedDocs` (int, default `100`) — documents buffered in RAM before auto-flush
- `RamBufferSizeMb` (double, default `16.0`) — RAM buffer size for the index writer

### 4. Implement `LuceneDocumentStore`

**File: `ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs`**

**Internal design:**
- Uses **one Lucene index directory** for all collections. Each document has a `_collection` field and a `_id` field for routing.
- Stores the full serialized JSON in a `_json` stored field (not indexed).
- Indexes all top-level properties as Lucene fields for querying:
  - String properties → `StringField` (exact match) + `TextField` (analyzed, for text search)
  - Numeric properties → `Int64Field` / `DoubleField`
  - Boolean properties → `StringField` with `"true"` / `"false"`
  - DateTime properties → `Int64Field` storing ticks
  - Enum properties → `StringField` storing the enum name
- **Counter storage** — counters are stored as documents in a `_counters` collection with `_id`, `Count` (Int64Field), and `WindowStart` (Int64Field).

**Key implementation details:**

**Constructor**:
- Takes `LuceneStoreOptions` (or just `string indexDirectory` for simplicity)
- Opens a `FSDirectory` pointing to the index directory
- Creates an `IndexWriter` with `StandardAnalyzer` (Lucene version `LUCENE_48`)
- The writer is kept open for the lifetime of the store (singleton)

**`GetAsync<T>`**: Query by `_collection` + `_id`, read `_json`, deserialize.

**`GetAllAsync<T>`**: Query by `_collection`, read all `_json` fields, deserialize.

**`SetAsync<T>`**: Delete existing doc (by `_collection` + `_id`), index new doc with all fields. Commit.

**`DeleteAsync`**: Delete by `_collection` + `_id`. Commit.

**`SearchAsync<T>`**: Build a `BooleanQuery`:
- Always filter by `_collection`
- For each `FilterClause`, translate to the appropriate Lucene query:
  - `Equals` → `TermQuery`
  - `Contains` → `WildcardQuery` with `*value*` or `PhraseQuery` on the text field
  - `StartsWith` → `PrefixQuery`
  - `GreaterThan/LessThan` → `NumericRangeQuery`
- For `TextSearch`, use `QueryParser` on the `_all_text` field (a concatenation of all string fields)
- Apply sort using Lucene `Sort` with `SortField`
- Use `IndexSearcher.Search` with `TopDocs` for pagination (`Skip` → `numHits = skip + take`, then skip results)
- Return `SearchResult<T>` with `TotalHits` for count

**`CountAsync<T>`**: Same query as `SearchAsync` but only need `TotalHits`.

**Counter methods**: Implement using the same pattern as `JsonFileDocumentStore` but storing counters as Lucene documents. Use a write lock for atomic increment/decrement (Lucene doesn't support atomic field updates, so read-modify-write under lock).

**Disposal**: Implement `IDisposable` — close the `IndexWriter` and `FSDirectory`.

### 5. Register Lucene provider in DI wiring

**File: `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs`**

In the `CreateDocumentStore` helper (from multi-provider plan), add a case for `PersistenceProvider.Lucene`:

```csharp
case PersistenceProvider.Lucene:
    return new LuceneDocumentStore(binding.Lucene?.IndexDirectory ?? "./lucene-index");
```

### 6. Add `Lucene` property to `StorageRoleBinding`

**File: `ClientManager.Api/Models/Configuration/StorageRoleBinding.cs`**

Add `LuceneStoreOptions? Lucene` property alongside the existing `MongoDb`, `Redis`, `JsonFile` properties.

### 7. Add `DefaultLucene` property to `PersistenceOptions`

**File: `ClientManager.Api/Models/Configuration/PersistenceOptions.cs`**

Add `LuceneStoreOptions? DefaultLucene` alongside `DefaultMongoDb`, `DefaultRedis`, `DefaultJsonFile`.

### 8. Update validation to handle Lucene provider

**File: `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs`**

In the startup validation (from multi-provider plan step 4), add validation for the Lucene provider:
- `IndexDirectory` must be non-empty
- The directory must be writable (or creatable)

## Verification

- Project compiles without errors
- `LuceneDocumentStore` can be instantiated with a test directory
- Basic CRUD works: `SetAsync` a document, `GetAsync` retrieves it, `DeleteAsync` removes it
- `SearchAsync` with a `FilterClause(FieldName: "Name", Operator: Contains, Value: "test")` returns matching documents
- `SearchAsync` with `TextSearch: "some text"` returns documents containing that text in any string field
- Counter operations work: `IncrementCounterAsync`, `GetCounterAsync`, `ResetCounterAsync`
- App starts successfully with `"DefaultProvider": "Lucene"` in config
- Index files are created in the configured directory
- **UI: If configured with Lucene as the default provider, navigate to the dashboard — verify all data loads**
- **UI: Navigate to the Clients page — verify clients appear and can be searched**
