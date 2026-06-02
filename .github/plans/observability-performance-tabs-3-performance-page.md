# Plan: Observability & Performance Sidebar Tabs — Step 3: Performance Page

> **Status**: 🔲 Not started
> **Prerequisite**: [observability-performance-tabs-2-observability-page.md](observability-performance-tabs-2-observability-page.md)
> **Next**: None — this is the final step.
> **Parent**: [observability-performance-tabs-overview.md](observability-performance-tabs-overview.md)

## TL;DR

Add the **Performance** sidebar entry and `/performance` page: two main latency charts (hot-path vs rest-of-service) over the selected time range, driven by two dropdowns — a **service** selector (`ClientManager.Api` / `ClientManager.StorageApi`) and an **aggregation** selector (Median / Average / p90 / p95 / p99). Below them, data-driven small square cards show per-operation latency over time for the selected service (Storage → `get` / `set` / `get_many` / `set_many` / …; API → its own operations). All latency values are computed from Jaeger span durations. When Jaeger is unavailable, show the shared notice.

## Iteration Bootstrap

- **Iteration slug**: `observability-performance-tabs`
- **Required evidence**: `dotnet build ClientManager.AdminUI` succeeds; `/performance` renders two latency charts that change with the service and aggregation dropdowns; per-operation cards appear and match the selected service (Storage shows `get`/`set`/`get_many`/`set_many`); Jaeger-down shows the friendly notice.
- **UI artifacts to verify**: `/performance` two-chart layout, both dropdowns, per-operation cards for each service, the unavailable-notice state. Sidebar shows the new "Performance" entry.
- **Commit-splitting guidance**: Single commit is acceptable.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor):
- The chart-card layout: `cm-monitor__chart-card`, `cm-dashboard__chart-header`, `cm-dashboard__filters` with `RadzenDropDown`s, and `ChartSettingsDropdown` on the right. Mirror this, but render **two** `RadzenChart`s (hot path + rest) instead of one.
- `RadzenChart` + `RadzenLineSeries`/`RadzenAreaSeries` with `RadzenCategoryAxis`/`RadzenValueAxis`, axis-scale transform helpers, the polling timer (`StartTimer`, `OnVisibilityChanged`), `OnTimeRangeChanged`, and the dropdown `Change` → reload pattern. Reuse all of it.

In [ClientManager.AdminUI/Services/ChartBucketAggregator.cs](ClientManager.AdminUI/Services/ChartBucketAggregator.cs):
- Time-bucketing of points into a chart-ready series. Reuse for bucketing span durations across the time range.

In [ClientManager.AdminUI/Services/JaegerApiService.cs](ClientManager.AdminUI/Services/JaegerApiService.cs) (from Step 1):
- `GetServicesAsync`, `GetOperationsAsync`, `SearchTracesAsync` — the data sources for this page.

There is **no existing percentile/latency-aggregation helper** in the repo; build a small dedicated one (Step 2 below).

## Steps

### 1. Add the sidebar entry

Edit [ClientManager.AdminUI/Components/Layout/NavMenu.razor](ClientManager.AdminUI/Components/Layout/NavMenu.razor). Add a "Performance" `NavLink` with `href="performance"` and a suitable `RadzenIcon` (e.g. `speed` is taken by Rate Limits — use `timeline` or `query_stats`), grouped with Observability from Step 2.

### 2. Create the aggregation + classification helpers

Create [ClientManager.AdminUI/Services/LatencyAggregator.cs](ClientManager.AdminUI/Services/LatencyAggregator.cs):
- An `enum LatencyAggregation { Median, Average, P90, P95, P99 }` (separate file under `Models/`).
- A method `double Compute(IReadOnlyList<double> durationsMs, LatencyAggregation mode)` — median/avg/percentile (linear interpolation) over a sorted copy. Keep under 30 lines; early returns for empty input.
- A method to bucket spans into a time series: given spans (start time + duration), a `from`/`to`, and a granularity, group by bucket and apply `Compute` per bucket. Reuse `ChartBucketAggregator` shapes where possible.

Create a small hot-path classifier (same file or a sibling): `bool IsHotPath(string operationName)` matching a single configurable pattern list (default: `operationName` starts with `storage.document_store.`, or matches the access-check / resource-acquire / resource-release / rate-limit span names). Keep the patterns in one `static readonly` collection so they are easy to adjust.

### 3. Create per-operation card data model

Add a small `record` (under `Models/Jaeger/` or page-local) for a per-operation card: operation name, the bucketed latency series (for the selected aggregation), and a summary value (latest or overall aggregation). Used to render each square card.

### 4. Create the Performance page

Create [ClientManager.AdminUI/Components/Pages/Performance/Performance.razor](ClientManager.AdminUI/Components/Pages/Performance/Performance.razor) with `@page "/performance"`:
- Inject `JaegerApiService`, `LatencyAggregator`, `EntityColorService`; reuse `ChartSettingsDropdown` and the polling timer.
- On init: `GetServicesAsync` to populate the **service** dropdown (default `ClientManager.StorageApi`); build the **aggregation** dropdown from `LatencyAggregation` values.
- Load: `SearchTracesAsync` for the selected service over the time range (large `limit`), flatten spans, classify each as hot-path or rest, and build two bucketed latency series via `LatencyAggregator`.
- Render two chart cards: "Hot path" and "Rest of service", each a `RadzenChart` line/area series over time. Both dropdowns live in the shared header and re-trigger the load on `Change`.
- If `JaegerResult.IsAvailable` is `false`, render `<JaegerUnavailableNotice Message="@_message" />` instead of the charts.
- Surface the approximate/sample-based caveat as small helper text near the charts.

### 5. Render data-driven per-operation cards

Below the two main charts, lay out small square card graphs (CSS grid of `cm-*` cards):
- Call `GetOperationsAsync(selectedService)` to discover operations; for each, build a per-operation bucketed series using the selected aggregation and render a compact `RadzenChart` (mini line/area) plus the operation name and summary value.
- Because this is data-driven, Storage automatically shows `get` / `set` / `get_many` / `set_many` (from `storage.document_store.*` operations) and API shows its own operations — no hard-coded metric list.
- Cap the number of cards (e.g. top-N by sample count) to keep the layout tidy; note the cap in helper text.

### 6. Wire dropdown + refresh behavior

- Changing the **service** dropdown reloads traces, both main charts, and regenerates the per-operation cards for that service.
- Changing the **aggregation** dropdown recomputes all series from the already-loaded spans (no refetch needed) — keep raw span samples in state to avoid extra Jaeger calls.
- The polling timer refreshes spans on the selected interval, matching `ActiveAllocations`.

## Verification

- `dotnet build ClientManager.AdminUI/ClientManager.AdminUI.csproj` compiles without errors.
- Unit-style check (optional but recommended): a small test or scratch probe confirms `LatencyAggregator.Compute` returns correct median/avg/p90/p95/p99 for a known sample set.
- **UI: With Jaeger, Storage API, API, AdminUI, and the traffic generator running, navigate to `/performance` — verify two latency charts (hot path + rest) render over the selected time range.**
- **UI: Change the aggregation dropdown (Median → Average → p90 → p95 → p99) — verify both charts and the cards recompute (p99 ≥ p95 ≥ p90 ≥ median ordering looks sane).**
- **UI: Change the service dropdown to `ClientManager.StorageApi` — verify per-operation cards include `get` / `set` / `get_many` / `set_many`; switch to `ClientManager.Api` — verify the cards change to API operations.**
- **UI: Stop Jaeger and reload `/performance` — verify the friendly `JaegerUnavailableNotice` appears (no error banner / stack trace).**
- **UI: Confirm the new "Performance" entry appears in the sidebar and routes correctly.**
- **UI: Take screenshots of the two-chart layout, the per-operation cards for both services, and the unavailable notice.**
