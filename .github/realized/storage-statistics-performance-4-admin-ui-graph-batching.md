# Plan: Storage Statistics Performance — Step 4: Admin UI Graph Batching

> **Status**: ✅ Completed
> **Prerequisite**: [storage-statistics-performance-3-batch-history-api-contract.md](storage-statistics-performance-3-batch-history-api-contract.md)
> **Next**: [storage-statistics-performance-5-performance-verification.md](storage-statistics-performance-5-performance-verification.md)
> **Parent**: [storage-statistics-performance-overview.md](storage-statistics-performance-overview.md)

## TL;DR

Change the Admin UI graph pages so they fetch long-range chart data in batches instead of starting one HTTP request per client. This removes the UI-side amplification that currently helps push storage reads past the API timeout budget.

## Reference Pattern

In [../../ClientManager.AdminUI/Services/StatisticsApiService.cs](../../ClientManager.AdminUI/Services/StatisticsApiService.cs):
- Keep public API URL construction centralized in this service.
- Follow the existing `GetHistoricalUsageAsync` query-string style for new history methods.

In [../../ClientManager.AdminUI/Components/Pages/Dashboard.razor](../../ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- Preserve timestamp-based chart bucketing through `ChartBucketAggregator`.
- Keep line/area chart shaping in the page, but stop issuing one history request per client.

In [../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor) and [../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor):
- Reuse the same batched history service methods so all chart pages benefit from the storage/API work.

## Steps

### 1. Add an Admin UI service method for batched client history

Edit [../../ClientManager.AdminUI/Services/StatisticsApiService.cs](../../ClientManager.AdminUI/Services/StatisticsApiService.cs) and add `GetHistoricalUsageByClientAsync` that calls `api/v1/statistics/historical-usage/by-client`.

Keep URL encoding consistent with `GetHistoricalUsageAsync`, and return an empty list if the API response is null.

### 2. Collapse Dashboard single-target per-client history calls

Edit [../../ClientManager.AdminUI/Components/Pages/Dashboard.razor](../../ClientManager.AdminUI/Components/Pages/Dashboard.razor). In `LoadSingleTargetChartDataAsync`, replace the `clientsToQuery.Select(... GetHistoricalUsageAsync ...)` plus `Task.WhenAll` pattern with one `GetHistoricalUsageByClientAsync` call for the selected target and selected clients.

Group the returned rows by client ID, then feed each client's points into the existing `ChartBucketAggregator` path. Keep the top-N aggregation and cap-line logic intact.

### 3. Collapse Dashboard all-target donut calls

In `LoadChartDataAsync` for the `AllTargetsId` branch, remove the per-client `GetHistoricalUsageAsync` fan-out used only for donut totals. Prefer `GetClientUsageBreakdownAsync` when it already returns the aggregate values needed for the selected targets and client filter; otherwise use one `GetHistoricalUsageByClientAsync` call and aggregate locally.

Do not add another per-client loop for the all-target case.

### 4. Update Monitor and Allocations chart consumers

Search the Admin UI pages for remaining `Task.WhenAll` or `Select` patterns that call `GetHistoricalUsageAsync` per client. Update [../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor) and [../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor) to use the new batched method where they need per-client series.

Leave aggregate target history calls on `GetHistoricalUsageAsync` when they are already a single batched target request.

### 5. Guard against stale long-running chart loads

If the affected pages can start a new chart load while an older one is still in flight, add a lightweight load version check before assigning chart state. Keep this local to the page and avoid broad cancellation plumbing unless it is already present nearby.

## Verification

- `dotnet build ClientManager.AdminUI/ClientManager.AdminUI.csproj`
- Browser: Navigate to `/`, select `Last 7 days`, and verify the usage-over-time chart and usage-per-client donut both render without an error alert.
- Browser: On `/`, select `Last 90 days`, switch between `Service` and `Resource Pool`, and verify the chart updates without text overlap, blank chart failure, or `Unable to load data`.
- Browser: Navigate to `/monitor`, select long ranges that use hour/day granularity, and verify charts render while live traffic continues.
- Browser: Navigate to `/allocations`, select long ranges for all pools, and verify active allocation charts and breakdowns render.
- While browser verification is running, watch the traffic generator output and confirm it does not switch into repeated `503` responses during graph loads.