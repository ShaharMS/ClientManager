# Plan: Search, Query Layer & Lucene Store

## Status: ✅ All steps completed

## Overview

Currently, **every filtered query** in the system follows the same anti-pattern: call `GetAllAsync()` to load the entire collection into memory, then filter with LINQ. This is visible in `GlobalRateLimitDatabase.GetByTargetAsync`, `UsageSnapshotDatabase.GetByTargetAsync`, `ResourceAllocationDatabase.GetActiveCountsByPoolAsync`, and every controller `GetAll` action that accepts filter parameters (`name`, `isEnabled`, `targetType`). At small scale this works, but it becomes a serious bottleneck as collections grow.

This plan addresses the problem in three layers:
1. **Query abstraction** — add a `SearchAsync<T>` capability to the `IDocumentStore` interface with a composable query model, so filtering can be pushed down to the storage engine.
2. **Lucene.NET store** — implement a new `LuceneDocumentStore` that provides a PVC-friendly, file-based, natively-searchable store. This replaces the `JsonFileDocumentStore` as the recommended local/PVC option.
3. **Push filtering down** — migrate all database implementations and controllers from GetAll+filter to use the new search capability, falling back to in-memory filtering only for stores that don't support native queries.

**Dependency**: This plan depends on the [multi-provider-storage-overview.md](multi-provider-storage-overview.md) plan being complete. The Lucene store needs to be registerable as a storage role provider, and the per-platform options pattern established in that plan is reused for `LuceneStoreOptions`.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [search-query-layer-1-query-abstraction.md](search-query-layer-1-query-abstraction.md) | Define `DocumentQuery<T>` model and add `SearchAsync` + `CountAsync` to `IDocumentStore` |
| 2 | [search-query-layer-2-lucene-store.md](search-query-layer-2-lucene-store.md) | Implement `LuceneDocumentStore` with native full-text and field search |
| 3 | [search-query-layer-3-existing-store-search.md](search-query-layer-3-existing-store-search.md) | Native search for MongoDB and Redis (RediSearch); documented in-memory fallback for JsonFile |
| 4 | [search-query-layer-4-database-migration.md](search-query-layer-4-database-migration.md) | Migrate all `*Database` implementations to use `SearchAsync` instead of `GetAllAsync` + LINQ |
| 5 | [search-query-layer-5-controller-search.md](search-query-layer-5-controller-search.md) | Add search parameters to controller endpoints and push them to the database layer |
| 6 | [search-query-layer-6-ui-search.md](search-query-layer-6-ui-search.md) | Add search fields to admin UI list pages and wire them to the API |

## Key Decisions

- **Query model over expression trees** — `DocumentQuery<T>` uses a list of field-level `FilterClause` objects (field name, operator, value) rather than `Expression<Func<T, bool>>`. Expression trees are hard to translate to Lucene queries and MongoDB filters. A simple clause-based model is translatable to every backend.
- **`SearchAsync` as a new method, not replacing `GetAllAsync`** — `GetAllAsync` remains for cases that genuinely need the full collection (rollup/prune cycles, reconciliation). `SearchAsync` is the new default for filtered reads.
- **`CountAsync` paired with `SearchAsync`** — many endpoints need only a count (e.g., active allocation counts). Having `CountAsync` with the same query model avoids loading documents just to count them.
- **Lucene.NET over LiteDB** — Lucene.NET excels at field-level filtering and full-text search, which is exactly what the search use case needs. LiteDB is simpler but doesn't offer comparable search capabilities. Lucene.NET's file-based index sits naturally on a PVC.
- **In-memory fallback for unsupported stores** — If a store doesn't implement native search for a particular query, it falls back to `GetAllAsync` + in-memory filtering. `JsonFileDocumentStore` always uses this path (it's inherently in-memory). `RedisDocumentStore` falls back to it when the RediSearch module isn't available. This keeps the system functional everywhere while enabling native search where the backend supports it.
- **Redis uses RediSearch module (not bare hashes)** — Bare Redis hashes don't support server-side filtering, but the RediSearch module (`FT.SEARCH`) provides full field-level and text search natively. The `RedisDocumentStore` detects whether the module is loaded at startup and uses it when available, falling back to in-memory otherwise. This requires `NRedisStack` alongside `StackExchange.Redis`.
- **`PersistenceProvider` enum extended with `Lucene`** — A new enum value is added to `PersistenceProvider` so Lucene can be selected as a storage role provider.
