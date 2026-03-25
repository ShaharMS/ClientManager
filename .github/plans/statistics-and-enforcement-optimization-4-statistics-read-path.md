# Plan: Statistics and Enforcement Performance Optimization — Step 4: Statistics Read Path

> **Status**: 🔲 Not started
> **Prerequisite**: [statistics-and-enforcement-optimization-3-usage-storage.md](statistics-and-enforcement-optimization-3-usage-storage.md)
> **Next**: None — this is the final step.
> **Parent**: [statistics-and-enforcement-optimization-overview.md](statistics-and-enforcement-optimization-overview.md)

## TL;DR

Make the dashboard and analytics endpoints fast by switching from ad-hoc aggregation over broad collections to targeted repository calls and materialized current-state summaries. This step turns the storage work from Step 3 into predictable UI-facing response times.

## Reference Pattern

[../../ClientManager.Api/Services/StatisticsService.cs](../../ClientManager.Api/Services/StatisticsService.cs) already centralizes statistics assembly and uses short-lived in-memory caching.

In [../../ClientManager.Api/Services/StatisticsService.cs](../../ClientManager.Api/Services/StatisticsService.cs):
- Keep composition logic in the service layer instead of moving it into controllers.
- Preserve the current cache-wrapper pattern, but invalidate based on data freshness instead of broad collection rescans.

[../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor) and [../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor) show the two most demanding UI pages that poll and fan out current statistics.

In [../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor):
- Preserve the filter-driven chart and table interactions.
- Optimize the backing API shape before changing the page behavior.

## Steps

### 1. Batch and materialize the “current state” statistics used by the UI

Refactor `ClientManager.Api/Services/StatisticsService.cs` and related models so dashboard and monitor-style queries can read a small current-state summary instead of recomputing from all snapshots and allocations on every request.

That materialized summary should cover at least:

- Current requests-per-minute and pool-acquisition headline values for `/`.
- Current service usage/cap summary for `/monitor`.
- Current pool usage/cap summary for `/allocations`.

Populate or refresh this read model in background flows that already process usage or allocation changes rather than on-demand in the controller.

### 2. Replace broad repository fan-out with targeted batch queries

Update `ClientManager.Api/Services/StatisticsService.cs`, `ClientManager.Api/Controllers/StatisticsController.cs`, and any related API models so list endpoints stop doing repeated per-target lookups and instead ask for batched results.

Specifically remove patterns where the controller or service loops through targets and repeatedly calls repository methods that each trigger their own store access.

### 3. Invalidate caches based on data version, not just fixed time

Keep short-lived caching, but tie invalidation to data refresh events from usage persistence and allocation updates. This should reduce unnecessary recomputation while avoiding stale results after a flush or release.

Keep the public API contracts stable unless a shape change is necessary for batching. If a response shape must change, update the Admin UI service layer in the same step rather than pushing transformation complexity into the pages.

### 4. Re-test the polling-heavy UI routes end to end

Review `ClientManager.AdminUI/Services/StatisticsApiService.cs` and the polling pages under `ClientManager.AdminUI/Components/Pages` to ensure the optimized endpoints are used consistently and no page falls back to more expensive legacy calls.

Avoid increasing client-side polling frequency to compensate for slower server work; the server path should become cheap enough that the existing polling intervals remain sufficient.

## Verification

- The API and Admin UI projects build without errors.
- Benchmark measurements show improved response times for dashboard, monitor, allocations, and historical usage endpoints compared with the Step 1 baseline.
- The monitor endpoints still return the same totals and caps for a known traffic sample after the read-path refactor.
- UI: Navigate to `/`, verify stat cards populate quickly, and confirm the usage chart and client overview render without error banners.
- UI: Navigate to `/monitor`, change the selected service and client filters, wait for at least one polling refresh, and verify both charts and tables update correctly.
- UI: Navigate to `/allocations`, change the selected pool and client filters, wait for refresh, and verify the chart, client detail grid, and all-pools grid stay consistent.
- UI: Capture a screenshot of `/monitor` and `/allocations` to confirm there is no visual regression, missing legend data, or empty-state bug after the API changes.