# Plan: Storage Statistics Performance — Step 5: Performance Verification

> **Status**: ✅ Completed
> **Prerequisite**: [storage-statistics-performance-4-admin-ui-graph-batching.md](storage-statistics-performance-4-admin-ui-graph-batching.md)
> **Next**: None — this is the final step.
> **Parent**: [storage-statistics-performance-overview.md](storage-statistics-performance-overview.md)

## TL;DR

Add a repeatable performance check for long-range graph reads under live traffic and use logs/browser testing to prove the storage circuit no longer opens. This step turns the optimization into something the next agent can verify instead of eyeballing one manual page load.

## Reference Pattern

In [../../_scripts/performance_baseline.py](../../_scripts/performance_baseline.py):
- Follow the existing standard-library-only Python style.
- Continue reporting JSON summaries with per-operation latency, success counts, and storage-size context.

In [../../_scripts/traffic_generator.py](../../_scripts/traffic_generator.py):
- Reuse the seeded client/service/pool IDs instead of hard-coding new fixture IDs.
- Keep the generator as live background load, not as the benchmark reporter.

In [../../.github/copilot-instructions.md](../../.github/copilot-instructions.md):
- Follow the documented local startup and shutdown order so the traffic generator does not flood the terminal during teardown.

## Steps

### 1. Extend the performance baseline with graph scenarios

Edit [../../_scripts/performance_baseline.py](../../_scripts/performance_baseline.py) to add explicit graph-read samples for long ranges. Include at least:

- Aggregate historical usage for all services over seven days.
- Aggregate historical usage for all resource pools over ninety days.
- Batched per-client historical usage for one service over seven days.
- Batched per-client historical usage for all pools over thirty or ninety days.

Keep these scenarios configurable with arguments such as `--include-graph-reads` and `--graph-ranges` so routine baselines can stay short.

### 2. Report storage circuit symptoms in the benchmark summary

Track status codes for graph scenarios and normal runtime operations separately. The JSON summary should make it obvious when graph reads caused unrelated access checks or resource acquisitions to return `503`.

Add fields such as `service_unavailable_count`, `graph_p95_latency_ms`, and per-scenario `max_latency_ms` using the existing summary style.

### 3. Add a log-based verification note

Update [../../.github/testing-checklist.md](../../.github/testing-checklist.md) or add a short section to the script output instructions describing how to sample current logs after a benchmark run. The check should look for recent `historical-usage`, `client-usage-breakdown`, and `Storage API unavailable` entries in the StorageApi and Api logs.

Do not require loading whole log files; current API logs can be very large. Use bounded tail/search commands in the instructions.

### 4. Run the local verification profile

Using the startup order from [../../.github/copilot-instructions.md](../../.github/copilot-instructions.md), run StorageApi, Api, AdminUI, seed data if needed, then run the traffic generator and the updated baseline profile. The expected result is that long-range graph reads complete, p95 graph latency is comfortably below the public API storage timeout after warm-up, and unrelated runtime operations do not show a burst of `503` responses.

## Verification

- `python _scripts/performance_baseline.py --base-url http://localhost:5062 --duration-seconds 60 --include-graph-reads`
- Confirm the benchmark JSON reports zero storage `503` responses for access checks and resource operations during graph reads.
- Confirm graph-read p95 latency and max latency are recorded for seven-day and ninety-day scenarios.
- Inspect recent StorageApi logs and verify `historical-usage` and `client-usage-breakdown` requests stay below the API timeout budget under the seeded workload.
- Browser: Navigate to `/`, `/monitor`, and `/allocations` after the benchmark and verify all charts still render with live data.
- Browser: On `/`, select `Last 90 days` while the traffic generator is running and verify no `Unable to load data` alert appears and no layout breakage is visible.