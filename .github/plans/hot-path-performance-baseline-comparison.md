# Hot Path Performance Baseline Comparison

## Run

- Before artifact: `.github/plans/hot-path-performance-baseline-before.json`
- Provisional artifact: `.github/plans/hot-path-performance-baseline-provisional.json`
- After artifact: `.github/plans/hot-path-performance-baseline-after.json`
- Captured: 2026-05-14, final verification pass
- Profile: seed 42, 60 seconds, 1,000,000 requests/day, 100 virtual clients, graph reads enabled for 7d range
- Background load: `traffic_generator.py --base-url http://localhost:5062 --interval 0.2`
- Runtime: StorageApi, Api, and AdminUI launched from source. StorageApi used JsonFile with `C:\Users\Marcus\source\repos\ClientManager\data` as the absolute data directory.

## Summary

- The after artifact is valid and contains nonzero hot-path counts: access 415, acquire 110, release 9.
- The success target is not met. Runtime unexpected failures increased from 24 before to 563 after, changing from 500 responses to 503 storage-unavailable responses.
- The old `_counters.json.tmp` failure signature did not recur. Log search found 0 `_counters.json.tmp` matches in Api and StorageApi logs.
- Access p95 regressed from 151.374 ms to 176.262 ms. Acquire p95 improved from 99.543 ms to 65.991 ms, but that result is not trustworthy because acquire had 99 unexpected 503s. Release p95 regressed from 101.346 ms to 5033.664 ms.
- Logs explain the remaining unexpected failures: StorageApi JsonFile writes to `UsageSnapshots` and `_counters` held locks long enough for public Api storage-client calls to hit the 5 second timeout, returning 503.
- Trace backend verification is unavailable in this environment. No local listeners were found on 4317, 4318, or 9200, and tracing exports only when `Observability:OtlpEndpoint` is configured. Local logs and `/prometheus/otel` metrics were used instead.

## Operation Comparison

| Operation | Count before | Count after | Avg before ms | Avg after ms | P95 before ms | P95 after ms | Max before ms | Max after ms | 429 before | 429 after | 500 before | 500 after | 503 before | 503 after |
|-----------|--------------|-------------|---------------|--------------|---------------|--------------|---------------|--------------|------------|-----------|------------|-----------|------------|-----------|
| access | 361 | 415 | 65.156 | 208.751 | 151.374 | 176.262 | 429.720 | 5029.651 | 4 | 0 | 8 | 0 | 0 | 368 |
| acquire | 123 | 110 | 55.404 | 200.187 | 99.543 | 65.991 | 199.393 | 5019.052 | 35 | 2 | 9 | 0 | 0 | 99 |
| release | 59 | 9 | 57.726 | 1872.313 | 101.346 | 5033.664 | 162.213 | 5033.664 | 0 | 0 | 7 | 0 | 0 | 2 |
| dashboard | 57 | 62 | 74.810 | 40.045 | 136.553 | 70.143 | 177.905 | 236.604 | 0 | 0 | 0 | 0 | 0 | 52 |
| monitor | 44 | 49 | 44.263 | 19.745 | 74.724 | 39.641 | 96.931 | 153.598 | 0 | 0 | 0 | 0 | 0 | 42 |

## Runtime Status Counts

| Artifact | 200 | 429 | 500 | 503 | Unexpected failures |
|----------|-----|-----|-----|-----|---------------------|
| before | 581 | 39 | 24 | 0 | 24 |
| after | 80 | 2 | 0 | 563 | 563 |

Graph read summary changed from all 50 graph requests returning 200 before to 9 successes and 40 503s after.

## Evidence

- Build: `dotnet build .\ClientManager.slnx` succeeded.
- Seed: `seed_data.py --base-url http://localhost:5062` completed and merged 5,727 usage snapshot documents, then wrote 679,486 historical buckets to `data\UsageSnapshots.json`.
- Warm-up before benchmark: 5 access, 3 acquire, and 3 release calls returned 200 after relaunching with production appsettings.
- Traffic generator: started with interval 0.2 and stopped before Api shutdown.
- Prometheus endpoints: `/prometheus/otel` responded for Api and StorageApi. Api exposed 7 custom `clientmanager_*` metrics, including access/resource/storage-client duration histograms. StorageApi exposed 15 custom metrics, including access/resource/rate-limit/document-store duration histograms.
- Logs: Api and StorageApi logs include non-empty `traceId`, `spanId`, and `correlationId` fields for hot-path requests and storage calls.
- Failure counts from logs: `_counters.json.tmp` matches 0; StorageApi `OperationCanceledException` matches 68; Api storage-unavailable/failure matches 1594.
- Representative Api log: trace `713bb609b53762b720c741bb57a20ba7` returned 503 for `/api/v1/access/check` after a 5023.4059 ms storage call timeout.
- Representative StorageApi log for the same trace: `_counters` `counter_increment` waited behind JsonFile locking and then failed with `OperationCanceledException` after 1509.4738 ms inside an access-check rate-limit path.
- Representative slow successful acquire: trace `c919bffa6f6d7293207ffb85d1fcddaf` acquired a resource after 4502.9946 ms at the public Api; StorageApi showed `UsageSnapshots` set at 4537.1028 ms and `_counters` counter increment with 4444.7711 ms lock wait.
- UI HTTP smoke: `/`, `/monitor`, and `/allocations` each returned 200 with no obvious error text.
- UI browser verification: screenshots were captured for `/`, `/monitor`, and `/allocations`, but all fresh browser views showed overlapping navigation text/labels and skeleton chart surfaces, so UI visual verification failed.
- Allocation state after generator stopped: resource-pool statistics reported 4 active allocations remaining.
- Shutdown: traffic generator was stopped first, then Api, StorageApi, and AdminUI. Ports 5062, 5063, and 5100 had no listeners after shutdown.

## Blockers And Risks

- Blocker: final success criteria are not satisfied because runtime unexpected failures are not zero. The failures are explained by logs, but the benchmark result is a regression from the accepted before artifact.
- Risk: JsonFile `UsageSnapshots` writes remain too slow for the configured public Api storage-client timeout under low-interval traffic, causing circuit-breaker/storage-unavailable behavior.
- Risk: Browser-visible AdminUI rendering is broken despite HTTP 200 route smoke.
- Risk: trace backend waterfall verification could not be performed without a configured/running OTLP collector or Elasticsearch backend.
