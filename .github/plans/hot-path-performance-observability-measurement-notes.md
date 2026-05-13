# Hot Path Performance Measurement Notes

These notes capture the exploratory measurement taken before the implementation plan was written.

## Provisional Baseline

Artifact: [hot-path-performance-baseline-provisional.json](.github/plans/hot-path-performance-baseline-provisional.json)

Context:
- StorageApi, Api, and AdminUI were already listening on ports 5063, 5062, and 5100.
- A fresh StorageApi start from the current checkout failed because [DocumentStoreFactory.cs](ClientManager.StorageApi/Utils/Extensions/DocumentStoreFactory.cs) calls a missing [LuceneDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs) constructor.
- [traffic_generator.py](_scripts/traffic_generator.py) ran with `--interval 0.2` and reached about 430 req/min before being stopped.
- [performance_baseline.py](_scripts/performance_baseline.py) had to be run with `--include-graph-reads --graph-ranges 7d` because the no-graph path can fall into an empty graph scenario branch.

Results:
- Runtime: 644 requests, 581 successes, 39 expected 429s, 24 unexpected 500s, average 62.04 ms, p95 128.149 ms.
- Access checks: 361 requests, average 65.156 ms, p95 151.374 ms, max 429.72 ms, 8 unexpected 500s.
- Resource acquires: 123 requests, average 55.404 ms, p95 99.543 ms, max 199.393 ms, 9 unexpected 500s.
- Resource releases: 59 requests, average 57.726 ms, p95 101.346 ms, max 162.213 ms, 7 unexpected 500s.
- Dashboard reads: 57 requests, average 74.81 ms, p95 136.553 ms.
- Monitor reads: 44 requests, average 44.263 ms, p95 74.724 ms.

## Failure Evidence

Recent StorageApi logs showed the 500s came from [JsonFileDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs):
- `System.IO.IOException: The process cannot access the file ... _counters.json.tmp because it is being used by another process` during resource release counter decrement.
- `System.UnauthorizedAccessException: Access to the path is denied` during rate-limit token bucket counter persistence.

The likely root cause is that multiple keyed JsonFile store instances point at the same data directory and therefore share `_counters.json` while each instance owns its own `_writeLock`.
