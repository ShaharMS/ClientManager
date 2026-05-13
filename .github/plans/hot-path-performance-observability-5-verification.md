# Plan: Hot Path Performance Observability — Step 5: Verification

> **Status**: 🔲 Not started
> **Prerequisite**: [hot-path-performance-observability-4-hot-path-logic.md](hot-path-performance-observability-4-hot-path-logic.md)
> **Next**: None — this is the final step.
> **Parent**: [hot-path-performance-observability-overview.md](hot-path-performance-observability-overview.md)

## TL;DR

Run the full before/after validation loop: build, launch all three apps, generate low-interval traffic, capture benchmark artifacts, inspect logs/traces, and verify the AdminUI. This step turns the performance work into evidence the user can inspect.

## Reference Pattern

Reuse the existing scripts and runtime conventions.

In [performance_baseline.py](_scripts/performance_baseline.py):
- The JSON output already includes operation latency, p95, max, status counts, and unexpected failure counts.
- Preserve this schema for artifact comparison.

In [traffic_generator.py](_scripts/traffic_generator.py):
- `--interval 0.2` creates the requested low-interval continuous load.
- The generator should be stopped before stopping Api or StorageApi.

In [RequestTrackingMiddleware.cs](ClientManager.Api/Middlewares/RequestTrackingMiddleware.cs):
- Request-level duration metrics already provide a cross-check against benchmark script timings.

In [nlog.config](ClientManager.Api/nlog.config):
- File logs are written under each app's output `logs` directory and include structured fields used to inspect failures.

## Steps

### 1. Build and launch from source

Run a clean build of the solution, then start StorageApi, Api, and AdminUI in the documented order. Use an absolute repo-root JsonFile data directory for StorageApi during local testing. Record terminal IDs or process IDs so the shutdown order is controlled.

### 2. Seed and warm the stack

Run [seed_data.py](_scripts/seed_data.py) through the public API. If it writes historical usage directly to disk, restart StorageApi after seeding only after Step 1's build/runtime issue is fixed. Make a few warm-up access and acquire requests before capturing measurements so caches and JIT compilation do not dominate the first samples.

### 3. Capture the final baseline under low-interval load

Start [traffic_generator.py](_scripts/traffic_generator.py) with `--interval 0.2`, then run [performance_baseline.py](_scripts/performance_baseline.py) for 60 seconds with the same seed and request rate used for the provisional artifact. Save an `after` JSON artifact under `.github/plans/` and preserve the Step 1 clean `before` artifact.

### 4. Compare before and after artifacts

Create a concise comparison of access checks, resource acquires, resource releases, runtime status counts, unexpected failures, dashboard reads, and monitor reads. Include average, p95, max latency, count, 429 count, and 500 count for each hot-path operation.

### 5. Inspect traces and logs

Use the configured trace backend, Prometheus endpoint, and NLog files to verify that one slow access check and one slow allocation acquire can be broken down by span: request received, public API storage client call, StorageApi service logic, rate-limit strategy, document-store wrapper, actual backend operation, usage recording, and response. Confirm there are no `_counters.json.tmp` storage exceptions.

### 6. Shut down cleanly

Stop [traffic_generator.py](_scripts/traffic_generator.py) first. Then stop Api, StorageApi, and AdminUI in that order unless the user wants the apps left running. Note any active allocations left by the traffic generator and whether cleanup/release handled them.

## Verification

- `dotnet build .\ClientManager.slnx` completes without errors before launch.
- StorageApi, Api, and AdminUI all respond on ports 5063, 5062, and 5100.
- [seed_data.py](_scripts/seed_data.py) completes successfully.
- [traffic_generator.py](_scripts/traffic_generator.py) reports roughly the expected low-interval traffic rate and is stopped before Api shutdown.
- [performance_baseline.py](_scripts/performance_baseline.py) writes a valid `after` artifact with nonzero access/acquire/release counts.
- Runtime unexpected failures are zero, or every remaining failure is linked to a trace/log explanation.
- Access-check and acquire p95 latencies are lower than the clean `before` artifact, or any regression is explained by trace evidence.
- `/prometheus/otel` exposes request and operation histograms for both Api and StorageApi.
- Trace backend shows full request waterfalls for access check and resource acquire.
- Logs contain traceId/spanId/correlationId for hot-path operations and storage calls.
- **UI: Navigate to `/` — verify the dashboard loads after the benchmark and take a screenshot.**
- **UI: Navigate to `/monitor` — verify live traffic is visible and no error banner or broken chart appears.**
- **UI: Navigate to `/allocations` — verify allocation state is coherent after the generator is stopped, including no obviously stale active counts.**
