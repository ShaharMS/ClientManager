# Hot Path Performance Baseline Comparison

## Run

- Before artifact: `.github/plans/hot-path-performance-baseline-before.json`
- Prior failed after artifact: `.github/plans/hot-path-performance-baseline-provisional.json`
- Latest after artifact: `.github/plans/hot-path-performance-baseline-after.json`
- Captured: 2026-05-14, Step 5 remediation verification pass
- Profile: seed 42, 60 seconds, 1,000,000 requests/day, 100 virtual clients, graph reads enabled for 7d range
- Background load: `traffic_generator.py --base-url http://localhost:5062 --interval 0.2`
- Runtime: StorageApi, Api, and AdminUI launched from source. StorageApi used JsonFile with `C:\Users\Marcus\source\repos\ClientManager\data` as the absolute data directory.

## Summary

- The latest after artifact is valid and contains nonzero hot-path counts: access 361, acquire 123, release 59.
- Runtime unexpected failures improved from 24 before and 563 in the prior failed after run to 0 in the latest after run.
- The old `_counters.json.tmp` collision signature did not recur; log search found 0 `_counters.json.tmp` matches.
- Access p95 improved from 151.374 ms before to 70.043 ms after. Acquire p95 improved from 99.543 ms to 80.647 ms. Release p95 improved from 101.346 ms to 50.572 ms.
- The latest after artifact contains no 500 or 503 responses. Expected 429 responses remain modeled client/rate-limit outcomes.
- Prometheus endpoints responded for both Api and StorageApi with custom `clientmanager_*` metrics and histogram buckets.
- Browser verification passed for `/`, `/monitor`, and `/allocations` after the AdminUI static web asset fix. Radzen CSS/JS returned HTTP 200, controls rendered normally, and chart surfaces loaded instead of staying in skeleton/unstyled states.
- Trace backend verification is unavailable in this environment. No local listeners were found on 4317, 4318, or 9200, and tracing exports only when `Observability:OtlpEndpoint` is configured. Local logs and `/prometheus/otel` metrics were used instead.

## Operation Comparison

| Operation | Count before | Count after | Avg before ms | Avg after ms | P95 before ms | P95 after ms | Max before ms | Max after ms | 429 before | 429 after | 500 before | 500 after | 503 before | 503 after |
|-----------|--------------|-------------|---------------|--------------|---------------|--------------|---------------|--------------|------------|-----------|------------|-----------|------------|-----------|
| access | 361 | 361 | 65.156 | 34.333 | 151.374 | 70.043 | 429.720 | 175.602 | 4 | 1 | 8 | 0 | 0 | 0 |
| acquire | 123 | 123 | 55.404 | 40.683 | 99.543 | 80.647 | 199.393 | 295.914 | 35 | 34 | 9 | 0 | 0 | 0 |
| release | 59 | 59 | 57.726 | 35.597 | 101.346 | 50.572 | 162.213 | 83.073 | 0 | 0 | 7 | 0 | 0 | 0 |
| dashboard | 57 | 57 | 74.810 | 68.768 | 136.553 | 135.954 | 177.905 | 230.690 | 0 | 0 | 0 | 0 | 0 | 0 |
| monitor | 44 | 44 | 44.263 | 42.046 | 74.724 | 73.609 | 96.931 | 190.059 | 0 | 0 | 0 | 0 | 0 | 0 |

## Runtime Status Counts

| Artifact | 200 | 429 | 500 | 503 | Unexpected failures |
|----------|-----|-----|-----|-----|---------------------|
| before | 581 | 39 | 24 | 0 | 24 |
| prior failed after | 80 | 2 | 0 | 563 | 563 |
| latest after | 609 | 35 | 0 | 0 | 0 |

Graph reads were healthy in the latest after artifact: 50 graph operations, 50 successes, status counts `{ 200: 50 }`, graph p95 1098.798 ms, and max 1416.437 ms.

## Evidence

- Build: `dotnet build .\ClientManager.slnx` succeeded after remediation with 31 pre-existing XML documentation warnings in StorageApi controllers.
- Targeted verifier: `dotnet run --project .\ClientManager.DataAccess.Tests\ClientManager.DataAccess.Tests.csproj` returned `JsonFile storage verification passed.`
- Seed/warm: full stack was seeded and warmed before the benchmark; access/acquire/release hot paths returned 200 during warm-up.
- Traffic generator: ran with interval 0.2 during the latest benchmark and was stopped before Api/StorageApi shutdown.
- Benchmark: `.github/plans/hot-path-performance-baseline-after.json` records 644 runtime operations, 609 successes, 35 expected 429s, zero unexpected failures, p95 85.122 ms, and max 295.914 ms.
- Prometheus: Api `/prometheus/otel` returned HTTP 200, length 2,908,903, with 9,755 `clientmanager_*` mentions and 8,979 bucket mentions. StorageApi `/prometheus/otel` returned HTTP 200, length 1,553,943, with 6,354 `clientmanager_*` mentions and 5,510 bucket mentions.
- Logs: Api and StorageApi logs include non-empty `traceId`, `spanId`, and `correlationId` fields for hot-path requests and storage calls. `_counters.json.tmp` matches remained 0.
- Log disposition: the latest benchmark artifact has no unexpected runtime failures. One post-benchmark `/api/v1/access/check` entry at 11:14:39 was a client-aborted `TaskCanceledException` after traffic termination, not a steady-state benchmark failure. The code now records request-aborted hot-path work as `canceled` instead of logging it as a server failure.
- UI assets: AdminUI production-style host returned HTTP 200 for `_content/Radzen.Blazor/css/material-base.css` and `_content/Radzen.Blazor/Radzen.Blazor.js` after `UseStaticWebAssets()` and `MapStaticAssets()` were enabled.
- UI browser verification: screenshots were captured for `/`, `/monitor`, and `/allocations`. Navigation labels no longer overlapped, Radzen controls rendered normally, and dashboard/monitor/allocation charts displayed loaded chart surfaces.
- Shutdown: traffic was stopped first. After UI asset remediation and verification, remaining hosts were stopped and ports 5062, 5063, and 5100 had no listeners.

## Blockers And Risks

- No active runtime or UI blocker remains in the latest after artifact and browser verification.
- Risk: JsonFile still rewrites large `UsageSnapshots` payloads, so slow `UsageSnapshots` operations remain visible as warnings. Batching and per-collection/counter locks keep those writes from blocking hot-path counters in the latest run.
- Risk: the cancellation log-classification cleanup was build-verified after the latest benchmark artifact, but the full 60 second benchmark was not rerun solely for that log-only cancellation outcome change.
- Risk: trace backend waterfall verification remains unavailable until a local OTLP collector or log/trace backend is configured.
