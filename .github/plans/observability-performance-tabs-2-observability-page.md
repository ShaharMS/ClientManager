# Plan: Observability & Performance Sidebar Tabs — Step 2: Observability Page

> **Status**: 🔲 Not started
> **Prerequisite**: [observability-performance-tabs-1-jaeger-foundation.md](observability-performance-tabs-1-jaeger-foundation.md)
> **Next**: [observability-performance-tabs-3-performance-page.md](observability-performance-tabs-3-performance-page.md)
> **Parent**: [observability-performance-tabs-overview.md](observability-performance-tabs-overview.md)

## TL;DR

Add the **Observability** sidebar entry and `/observability` page: a list-style layout (like Services/Clients/Resource Pools) with a search box that searches traces by trace ID and an ID-to-trace `RadzenDataGrid`. Each row is expandable (Radzen master/detail) to render a custom span-waterfall graphic for that trace. When Jaeger is unavailable, show the shared notice.

## Iteration Bootstrap

- **Iteration slug**: `observability-performance-tabs`
- **Required evidence**: `dotnet build ClientManager.AdminUI` succeeds; `/observability` lists recent traces; searching a known trace ID filters to that trace; expanding a row renders a span waterfall; Jaeger-down shows the friendly notice.
- **UI artifacts to verify**: `/observability` grid populated; one expanded row showing the waterfall; the unavailable-notice state. Sidebar shows the new "Observability" entry.
- **Commit-splitting guidance**: Single commit is acceptable.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Services/ServiceList.razor](ClientManager.AdminUI/Components/Pages/Services/ServiceList.razor):
- `@page` route, `cm-page-header`, `TableSkeleton` loading state, error alert, `cm-list-page__table-card` + `cm-list-page__header`, a `RadzenTextBox` search bound to a `_search` field filtered in a computed property, and a `RadzenDataGrid` with `AllowSorting`/`AllowPaging`. Mirror this layout exactly.
- The dynamic page-size pattern (`table.js`, `OnWindowResize`, `RecalculatePageSizeAsync`) is optional here — reuse it for consistency or use a fixed `PageSize`.

In [ClientManager.AdminUI/Components/Layout/NavMenu.razor](ClientManager.AdminUI/Components/Layout/NavMenu.razor):
- `NavLink` items with a `RadzenIcon` and label inside `cm-sidebar__nav-item`, grouped by `cm-sidebar__divider`. Add the new entry following this shape.

In [ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor):
- The visibility/polling timer pattern (`OnVisibilityChanged`, `StartTimer`, `_pollingInterval`) and `ChartSettingsDropdown` usage — reuse for auto-refreshing the trace list.

Radzen master/detail row expansion: use `RadzenDataGrid` with a `<Template Context="trace">` inside `<RadzenDataGridColumn>` for cells and a `<DetailTemplate Context="trace">` for the expandable waterfall (Radzen `ExpandMode` single/multiple).

## Steps

### 1. Add the sidebar entry

Edit [ClientManager.AdminUI/Components/Layout/NavMenu.razor](ClientManager.AdminUI/Components/Layout/NavMenu.razor). Add an "Observability" `NavLink` with `href="observability"` and a suitable `RadzenIcon` (e.g. `account_tree` or `lan`). Place it in a sensible section — recommend a new divider with Observability + Performance grouped together (these two are added in Steps 2–3).

### 2. Create the trace-row view model

Create a small `record` (under `ClientManager.AdminUI/Models/Jaeger/` or a page-local type) projecting a `JaegerTrace` into a grid row: trace ID, root operation name, root service, start time, total duration (ms), and span count. Compute the root span as the span with no `CHILD_OF` reference within the trace.

### 3. Create the Observability page

Create [ClientManager.AdminUI/Components/Pages/Observability/Observability.razor](ClientManager.AdminUI/Components/Pages/Observability/Observability.razor) with `@page "/observability"`:
- Inject `JaegerApiService` and reuse `ChartSettingsDropdown` (optional, for refresh-rate/time-range) and the polling timer.
- On init, call `SearchTracesAsync` for a default lookback across both services; map results to row view models.
- Header with a `RadzenTextBox` "Search by trace ID…". When the search value looks like a trace ID, call `GetTraceByIdAsync` and show just that trace; otherwise filter the loaded list by trace-ID substring (mirror `ServiceList`'s computed filter).
- If the `JaegerResult.IsAvailable` is `false`, render `<JaegerUnavailableNotice Message="@_message" />` instead of the grid.
- `RadzenDataGrid` columns: Trace ID, Root Operation, Service, Start Time, Duration (ms), Spans. Enable row expansion.

### 4. Create the span-waterfall component

Create [ClientManager.AdminUI/Components/Pages/Observability/TraceWaterfall.razor](ClientManager.AdminUI/Components/Pages/Observability/TraceWaterfall.razor) (+ optional `.razor.css`):
- `[Parameter] public JaegerTrace Trace { get; set; }`.
- Build the span tree from `references` (`CHILD_OF`), order by `startTime`, and compute each span's offset and width relative to the trace's min start and total duration.
- Render each span as a horizontal bar (Gantt/waterfall) using CSS (`position`/`width` percentages or flex), indented by depth, labeled with `operationName`, service, and duration. Color bars by service (reuse `EntityColorService` if convenient, keyed by `serviceName`).
- Keep the component under 200 lines; split tree-building into a small helper if needed (max 2 levels of nesting, early returns).

### 5. Wire the detail template

In `Observability.razor`, set the grid's `<DetailTemplate Context="trace">` to render `<TraceWaterfall Trace="@trace.Source" />` (where `Source` is the underlying `JaegerTrace` carried on the row view model). Lazy-fetch the full trace via `GetTraceByIdAsync` on expand if the search result didn't include all spans.

## Verification

- `dotnet build ClientManager.AdminUI/ClientManager.AdminUI.csproj` compiles without errors.
- **UI: Start Jaeger (`python _scripts/launch_observability_ui.py up`), the Storage API, API, AdminUI, and the traffic generator. Navigate to `/observability` — verify the grid lists recent traces with IDs, operations, and durations.**
- **UI: Type a known trace ID into the search box — verify the grid narrows to that single trace.**
- **UI: Expand a trace row — verify the span waterfall renders bars proportional to duration, indented by parent/child depth, labeled with operation/service/duration.**
- **UI: Stop Jaeger and reload `/observability` — verify the friendly `JaegerUnavailableNotice` appears (no error banner / stack trace).**
- **UI: Confirm the new "Observability" entry appears in the sidebar and routes correctly.**
- **UI: Take screenshots of the populated grid, an expanded waterfall, and the unavailable notice.**
