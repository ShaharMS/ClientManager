# Plan: Code Slim-Down — Step 2b: Storage Technology Bindings

> **Status**: ✅ Completed
> **Prerequisite**: [code-slimdown-2-dataaccess.md](code-slimdown-2-dataaccess.md)
> **Next**: [code-slimdown-3-storageapi-services.md](code-slimdown-3-storageapi-services.md)
> **Parent**: [code-slimdown-overview.md](code-slimdown-overview.md)

## TL;DR

Modernize the storage *bindings* — the concrete `IDocumentStore` implementations for Redis, MongoDB, Lucene, and JSON file, plus their factory and option classes. These are the least-maintainable surface in the solution: long constructors, repeated serialization/key-building/collection-mapping scaffolding, and verbose provider-config plumbing. Goal is fewer lines and cleaner shared structure without changing which provider does what. **Documentation stays as-is.**

## Iteration Bootstrap

- **Iteration slug**: `code-slimdown`
- **Required evidence**: `dotnet build ClientManager.DataAccess` clean; `ClientManager.DataAccess.Tests` pass (the behavior-parity gate covering all four providers); a JsonFile-backed round-trip works end-to-end via the running stack; `git diff --stat` net deletions.
- **UI artifacts to verify**: With the default (JsonFile) provider, AdminUI CRUD on a list page still persists and reloads correctly.
- **Commit-splitting guidance**: One commit per concern — (a) shared store helpers (serialization/key/collection mapping), (b) per-provider store cleanup, (c) `DocumentStoreFactory` builders, (d) store-option classes.

## Reference Pattern

In [ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs), [MongoDBDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs), [LuceneDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs), [JsonFileDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs):
- All four implement the same `IDocumentStore` surface (Get/GetAll/Set/Delete/Search/Count + counter ops). Each repeats `JsonSerializerOptions` setup, collection-name → backing-key mapping, counter-key prefixing, and the search/count fallback to `InMemoryQueryEvaluator`.

In [ClientManager.DataAccess/Stores/Implementations/Helpers/](ClientManager.DataAccess/Stores/Implementations/Helpers/):
- `InMemoryQueryEvaluator` and the shared-state helpers already exist — this is where additional shared store utilities belong.

In [ClientManager.StorageApi/Utils/Extensions/DocumentStoreFactory.cs](ClientManager.StorageApi/Utils/Extensions/DocumentStoreFactory.cs):
- `CreateMongoStore`/`CreateRedisStore`/`CreateLuceneStore`/`CreateJsonFileStore` each inline a long settings/cache block (settings builders also addressed in step 4 — coordinate to avoid overlap).

## Steps

### 1. Extract shared store scaffolding into helpers

Add helpers under [Stores/Implementations/Helpers/](ClientManager.DataAccess/Stores/Implementations/Helpers/) for the cross-provider repetition:
- A single shared `JsonSerializerOptions` instance (each store currently defines its own identical `PropertyNameCaseInsensitive = true`).
- A counter-key helper (`counter:` prefix / global prefix composition) currently duplicated across Redis/Json.
- The search-then-fallback-to-`InMemoryQueryEvaluator` pattern, if it can be factored without coupling providers.

Reduce each store to use the shared helpers. Keep provider-specific behavior (native `FT.SEARCH`, Mongo queries, Lucene index) intact.

### 2. Modernize per-provider store constructors and members

Convert each `DocumentStore` to a primary constructor where it reduces fields-boilerplate, use expression-bodied members for one-line properties (e.g., `Database => _redis.GetDatabase(...)`, `CollectionPath(...)`), collection expressions, and switch expressions. Flatten nesting to ≤2 levels with early returns. Do not alter serialization formats or key layouts (persisted data must still load) — or, if a cleaner key/format is worth a breaking change, gate it behind the 1.0.0 allowance and note it so seed/data files are regenerated.

### 3. Consolidate the `DocumentStoreFactory` create-methods

In [DocumentStoreFactory.cs](ClientManager.StorageApi/Utils/Extensions/DocumentStoreFactory.cs), the four `Create*Store` methods share the cache-lookup-or-build shape. Extract a small generic cache-or-create helper and the per-provider settings builders (the Mongo/Redis settings extraction is also referenced by step 4 — implement it here and have step 4 consume it). Reduce nesting and use collection expressions (e.g., `ClientCertificates = [cert]`).

### 4. Slim the store option classes

In [ClientManager.Shared/Configuration/Storage/](ClientManager.Shared/Configuration/Storage/) (`MongoDbStoreOptions`, `RedisStoreOptions`, `LuceneStoreOptions`, `JsonFileStoreOptions`, `StorageRoleBinding`, `PersistenceOptions`), convert option carriers to records or primary-constructor types where binding allows, and use collection-expression/target-typed defaults. **Keep configuration property names identical** so existing `appsettings*.json` binds unchanged (these names are a config contract; changing them is a breaking change to defer unless explicitly desired). Keep all existing documentation.

## Verification

- `dotnet build ClientManager.DataAccess` and `ClientManager.StorageApi` compile cleanly.
- `ClientManager.DataAccess.Tests` pass — covers all four provider behaviors and is the primary parity gate.
- Sample end-to-end: start the stack with the default JsonFile provider, create + read + update + delete an entity, and confirm the on-disk `data/*.json` round-trips.
- If MongoDB/Redis test coverage exists, run those provider tests; otherwise note them as manually unverified.
- `git diff --stat` shows net deletions.
- **UI: With the full stack on the JsonFile provider, create + edit + delete a Service via `/services` and confirm persistence and grid refresh (exercises the store binding read/write path).**
- **UI: Reload the page and confirm the data persisted (write-through cache + serialization intact).**
