# Plan: Code Slim-Down — Step 8: AdminUI

> **Status**: ✅ Complete
> **Prerequisite**: [code-slimdown-7-api-exceptions-instrumentation.md](code-slimdown-7-api-exceptions-instrumentation.md)
> **Next**: None — this is the final step.
> **Parent**: [code-slimdown-overview.md](code-slimdown-overview.md)

## TL;DR

Consolidate the highest-volume duplication in the Blazor AdminUI: a generic API service base for the four CRUD clients, a shared list-page template and editor-page template, a `StatusBadge` component, a reusable polling/visibility behavior, and deletion of confirmed-unused components. Lower priority than the backend steps but the single largest line-count opportunity in the solution.

## Iteration Bootstrap

- **Iteration slug**: `code-slimdown`
- **Required evidence**: `dotnet build ClientManager.AdminUI` clean; every page renders and CRUD works against the live API; `git diff --stat` net deletions.
- **UI artifacts to verify**: Dashboard, Clients, Services, Resource Pools, Rate Limits, Quotas, Monitor, Allocations — all render and remain interactive.
- **Commit-splitting guidance**: Separate commits for (a) generic API service base, (b) list-page template, (c) editor-page template + shared form models, (d) StatusBadge + formatters, (e) polling behavior extraction, (f) dead-code deletion.

## Reference Pattern

In [ClientManager.AdminUI/Services/ClientApiService.cs](ClientManager.AdminUI/Services/ClientApiService.cs), [ServiceApiService.cs](ClientManager.AdminUI/Services/ServiceApiService.cs), [ResourcePoolApiService.cs](ClientManager.AdminUI/Services/ResourcePoolApiService.cs), [GlobalRateLimitApiService.cs](ClientManager.AdminUI/Services/GlobalRateLimitApiService.cs):
- Identical GetAll(+cache)/Search/GetById/Create/Update/Delete/invalidate bodies — the generic base target.

In [ClientManager.AdminUI/Components/Pages/Clients/ClientList.razor](ClientManager.AdminUI/Components/Pages/Clients/ClientList.razor) and the other `*List.razor` pages:
- Same header/loading/error/search/grid/pagination/JS-resize/filter scaffolding — the list-template target.

In [ClientManager.AdminUI/Components/Pages/Services/ServiceEditor.razor](ClientManager.AdminUI/Components/Pages/Services/ServiceEditor.razor) and the other `*Editor.razor` pages:
- Same back-link/header/loading/form-wrapper/action-bar/load+save lifecycle — the editor-template target. `RateLimitFormModel` is duplicated in [RateLimitEditor.razor](ClientManager.AdminUI/Components/Pages/RateLimits/RateLimitEditor.razor) and [QuotaEditor.razor](ClientManager.AdminUI/Components/Pages/Quotas/QuotaEditor.razor).

## Steps

### 1. Generic API service base

Create `ClientManager.AdminUI/Services/GenericApiService.cs` implementing the shared CRUD + client-side cache against an injected `HttpClient` and a route prefix. Reduce the four concrete services to thin subclasses. Keep their public interfaces so component DI is unchanged.

### 2. List-page template

Extract a generic `ListPageTemplate<TEntity>` component (or shared base) handling header, loading/error states, search box (`@bind-Value` only — remove the redundant `@oninput`), pagination, JS table-resize, and the `_filtered` filter predicate via a supplied selector. Reduce each `*List.razor` to grid column definitions + load/delete delegates. Verify filter behavior (id/name contains, case-insensitive) is preserved on each page.

### 3. Editor-page template and shared form models

Extract a generic `EditorPageTemplate<TModel>` component handling back-link, header (`Create`/`Edit`), loading/error wrapper, action bar, and the load/save lifecycle. Reduce each `*Editor.razor` to its form fields + map-to/from-model logic. Move duplicated form models (`RateLimitFormModel`, and others shared across editors) into [ClientManager.AdminUI/Models/](ClientManager.AdminUI/Models/) as single definitions. Consider extracting `ClientEditor.razor` sub-sections (service-access, pool-access, rate-limit) into sub-components if it reduces lines without complicating binding.

### 4. StatusBadge component and shared formatters

Create a `StatusBadge` component that maps a status to color/text, replacing the repeated inline badge HTML in Monitor and ActiveAllocations. Create a `TimeSpanFormatter` static helper (and reuse the existing compact formatter) to replace the duplicated `FormatWindow`/`FormatTtl`/`FormatCompact` methods. Optionally add a small query-string builder for `StatisticsApiService` URL construction.

### 5. Polling/visibility behavior

Extract the duplicated timer + page-visibility + polling-interval logic (shared by Dashboard, Monitor, ActiveAllocations) into a reusable behavior class or component base. Preserve the JS interop module usage and interval-change handling. Keep the chart-series rendering loop extraction (`ClientAreaChartSeries`) optional if it cleanly reduces lines.

### 6. Delete confirmed dead code

Use find-all-references to confirm [TimeRangeSelector.razor](ClientManager.AdminUI/Components/TimeRangeSelector.razor) and [PollingIntervalSelector.razor](ClientManager.AdminUI/Components/PollingIntervalSelector.razor) are unused (functionality lives in `ChartSettingsDropdown`). Delete only if truly unreferenced. Remove any unused usings surfaced by the build.

## Verification

- `dotnet build ClientManager.AdminUI` compiles cleanly with no new warnings.
- `git diff --stat` shows net deletions (largest single-project reduction expected).
- **UI: With the full stack + traffic running, load every page — `/`, `/clients`, `/services`, `/resourcepools`, `/ratelimits`, `/quotas`, Monitor, Allocations — and confirm each renders with data and no console/error banners.**
- **UI: On a list page, type in the search box and confirm filtering works (list-template path).**
- **UI: Open an editor (e.g., `/services` → edit), change a field, save, and confirm it persists and the grid refreshes (editor-template path).**
- **UI: Confirm Monitor/Allocations status badges render with correct colors (StatusBadge) and charts update under traffic (polling behavior).**
- **UI: Take screenshots of the Dashboard and one editor to confirm no layout regressions.**
