# Plan: Harden API/Storage Split Reliability — Step 3: Read Models and Rollout

> **Status**: ✅ Completed
> **Prerequisite**: [api-storage-split-reliability-2-transport-contracts.md](api-storage-split-reliability-2-transport-contracts.md)
> **Next**: None — this is the final step.
> **Parent**: [api-storage-split-reliability-overview.md](api-storage-split-reliability-overview.md)

## TL;DR

Finish the hardening pass by validating statistics composition, cache ownership, configuration wiring, and load-path validation. This step turns the split from “architecturally separated” into “operationally believable” by checking the read-heavy surfaces and rollout assumptions that usually break after the runtime layer starts working.

## Reference Pattern

In [../../ClientManager.Api/Services/Implementations/StatisticsService.cs](../../ClientManager.Api/Services/Implementations/StatisticsService.cs):
- Treat the deleted service as the baseline for chart and summary semantics.
- Preserve fallback granularity behavior and avoid changing dashboard meaning while moving the implementation.

In [../../ClientManager.Api/Services/Implementations/Exporters/PrometheusExportService.cs](../../ClientManager.Api/Services/Implementations/Exporters/PrometheusExportService.cs):
- Keep emitted metrics stable unless there is a deliberate compatibility decision.
- Preserve gauge names, label sets, and fallback rules.

In [../../_scripts/seed_data.py](../../_scripts/seed_data.py):
- Use the existing local validation workflow rather than inventing a parallel one.
- Keep startup order and traffic-generation validation aligned with the repo instructions.

## Steps

### 1. Audit statistics and exporter parity against the deleted public-host implementations

Compare the new storage-host statistics and exporter code to the deleted `ClientManager.Api` versions. Fix any behavior drift in overview counts, granularity fallback, client summaries, Prometheus metrics, or Grafana JSON payloads.

### 2. Review cache ownership and invalidation under real write paths

Validate that catalog and statistics caches in `ClientManager.StorageApi` invalidate on every relevant write path and that no stale read survives longer than intended after client, service, pool, or global-rate-limit mutations.

### 3. Validate configuration and rollout assumptions explicitly

Inspect appsettings, launch settings, startup order, and local testing instructions together. Confirm the split’s environment assumptions are coherent: storage host reachable first, public host pointed at it, and production guidance not quietly normalizing unsafe JSON-file multi-instance behavior.

## Verification

- Statistics, Prometheus, and Grafana endpoints produce the same external payload semantics as the pre-split system.
- Cache invalidation occurs on every relevant catalog write and downstream dashboard reads update within the configured TTL boundaries.
- Local startup instructions and appsettings reflect the real two-host dependency order and do not imply unsupported production topology.
- UI: Navigate to `/` and `/monitor` and verify overview tiles, charts, and breakdowns still render with coherent live data under traffic.
- UI: Edit a client, service, resource pool, and global rate limit, then revisit the affected pages and verify stale catalog or dashboard data does not linger past the intended cache window.
- UI: Run the traffic generator and verify the dashboard, monitor view, and allocation pages stay responsive and internally consistent under sustained activity.