# Plan: Storage Statistics Performance

## Status: ✅ All steps completed

## Overview

Long-range graph reads currently fan out into many small storage lookups and can run longer than the public API's five-second storage-call budget. The current logs show `/internal/v1/statistics/historical-usage` taking about six seconds while the public API then opens the storage circuit and returns `503` responses with `Retry-After` for unrelated access checks, resource acquisitions, and dashboard reads. Prior work in `statistics-history-continuity` repaired correctness across rollups; this plan keeps that continuity behavior but changes how the same history is fetched and consumed so long windows like seven and ninety days do not starve the storage service.

The desired end state is a fast, batch-oriented statistics path. `ClientManager.DataAccess` should support direct multi-key reads for deterministic usage snapshot segment IDs, `ClientManager.StorageApi` should aggregate requested target/client history with request-scoped batches instead of nested sequential lookups, the public API should expose a client-batched history contract for chart consumers, and `ClientManager.AdminUI` should stop issuing one HTTP request per client when rendering graphs.

## Sub-Plans (execute in order)

| Order | Status | Plan File | Summary |
|-------|--------|-----------|---------|
| 1 | ✅ Completed | [storage-statistics-performance-1-batch-document-reads.md](.github/realized/storage-statistics-performance-1-batch-document-reads.md) | Add direct multi-key document reads and use them for usage snapshot segment retrieval. |
| 2 | ✅ Completed | [storage-statistics-performance-2-storage-history-aggregation.md](.github/realized/storage-statistics-performance-2-storage-history-aggregation.md) | Refactor storage-side statistics history aggregation to batch target/client/range reads per request. |
| 3 | ✅ Completed | [storage-statistics-performance-3-batch-history-api-contract.md](.github/realized/storage-statistics-performance-3-batch-history-api-contract.md) | Add internal and public API support for fetching per-client historical graph data in one request. |
| 4 | ✅ Completed | [storage-statistics-performance-4-admin-ui-graph-batching.md](.github/realized/storage-statistics-performance-4-admin-ui-graph-batching.md) | Update Admin UI graph pages to use batched history APIs and avoid per-client request storms. |
| 5 | ✅ Completed | [storage-statistics-performance-5-performance-verification.md](.github/realized/storage-statistics-performance-5-performance-verification.md) | Extend the performance baseline to exercise long-range graph reads and verify no storage circuit storm. |

## Key Decisions

- **Direct key batching before concurrency** — Usage snapshot segment IDs are deterministic, so prefer a storage abstraction that fetches many known IDs at once over adding unbounded `Task.WhenAll` fan-out inside the storage service.
- **Keep continuity semantics** — Preserve `StatisticsService.Continuity.cs` fallback behavior from the completed history-continuity plan; optimize how data is loaded, not which buckets are considered valid.
- **Client-batched API contract** — Add a new per-client history response instead of overloading `HistoricalUsageResponse`; existing consumers keep their contract while chart pages gain an efficient path.
- **Storage remains authoritative** — Aggregation by target, client, granularity, and bucket timestamp stays in `ClientManager.StorageApi`; the Admin UI only buckets already-composed responses for display.
- **Performance proof is required** — Verification must include long-range graph reads under live traffic and must check that unrelated access/resource requests do not receive `503` while graphs are loading.