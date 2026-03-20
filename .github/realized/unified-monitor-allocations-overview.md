# Plan: Unified Monitor & Allocations Screens

## Status: ✅ All steps completed

## Overview

The Monitor (`/monitor`) and Active Allocations (`/allocations`) pages currently have different layouts, filter controls, and chart types despite serving the same conceptual purpose — real-time target utilization monitoring. Monitor uses a single-service selector with a line chart; Allocations uses a pool filter with a stacked bar chart. The user wants both screens to follow an identical layout pattern, differentiated only by target type (Service vs ResourcePool).

The desired end state is two pages with the same structure: filter dropdowns at the top (target + client), stacked area charts per-target showing per-client usage over time with a rate-limit cap line, a per-client-per-target breakdown table with utilization bars, an `<hr>` separator, and a full all-targets summary table (unfiltered) with 0–100% utilization bars.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [unified-monitor-allocations-1-monitor.md](.github/plans/unified-monitor-allocations-1-monitor.md) | Redesign Monitor.razor to the unified layout (services context) |
| 2 | [unified-monitor-allocations-2-allocations.md](.github/plans/unified-monitor-allocations-2-allocations.md) | Redesign ActiveAllocations.razor to the same unified layout (resource pools context) |

## Key Decisions

- **Target dropdown is single-select with "All" option** — follows the existing Dashboard pattern (`AllTargetsId = "__all__"`). When "All" is selected, one chart card is rendered per target. When a specific target is selected, only that chart card is shown.
- **Client dropdown is multi-select** — already used in Monitor and Dashboard. Filters chart data and per-client-per-target table to selected clients only.
- **Chart type: `RadzenStackedAreaSeries`** — Radzen.Blazor 10.0.5 supports this. Each client gets a different-colored stacked filled area. A dashed `RadzenLineSeries` overlays the global rate limit cap (services) or max slots (pools).
- **Bottom all-targets summary is unfiltered** — not affected by target or client dropdown selections. Always shows every target with a 0–100% utilization progress bar.
- **No new API endpoints needed** — existing statistics endpoints (`usage-timeseries`, `historical-usage`, `client-usage-breakdown`, `resource-pools`) provide all required data. Per-client time-series data is fetched by calling `GetHistoricalUsageAsync` once per client (pattern already used by both pages).
- **Auto-refresh cadence** — both pages auto-refresh every 10 seconds (matching the current Allocations timer). Monitor's 30-second timer will be shortened to 10 seconds for consistency.
- **Consistent CSS classes** — both pages reuse existing `cm-monitor__filters`, `cm-monitor__chart-card`, `cm-list-page__table-card`, and `cm-dashboard__chart-header` classes. A new `cm-monitor__separator` class is added for the `<hr>` styling.
