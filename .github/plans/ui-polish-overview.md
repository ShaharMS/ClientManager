# Plan: UI Polish — Skeleton Loaders, Unified Charts & Deterministic Colors

## Status: � In progress

## Overview

The AdminUI currently uses a mix of loading indicators (plain `<p>Loading...</p>` text, Radzen circular spinners, Radzen indeterminate linear bars) spread across 13+ pages. This plan unifies all of them into a single skeleton loader pattern with a smooth gray-to-white shimmer animation.

Additionally, the Dashboard's "Usage Over Time" chart uses `RadzenLineSeries` while Monitor and Active Allocations use `RadzenStackedAreaSeries` with per-client breakdown. This plan aligns the Dashboard chart to use the same stacked-area pattern. Colors assigned to clients/services/pools are currently random per render; this plan introduces a deterministic hash-based color palette so the same entity always gets the same color across all pages and runtime instances. Finally, for scenarios with 100+ clients, the plan introduces a "top N + Others" aggregation strategy to keep charts readable.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [ui-polish-1-skeleton-foundation.md](.github/plans/ui-polish-1-skeleton-foundation.md) | Create skeleton CSS animation and reusable `SkeletonBlock` component |
| 2 | [ui-polish-2-skeleton-crud-pages.md](.github/plans/ui-polish-2-skeleton-crud-pages.md) | Replace `<p>Loading...</p>` on all CRUD list & editor pages |
| 3 | [ui-polish-3-skeleton-dashboard-monitor.md](.github/plans/ui-polish-3-skeleton-dashboard-monitor.md) | Replace circular/linear spinners on Dashboard, Monitor, and Allocations with contextual skeletons |
| 4 | [ui-polish-4-deterministic-colors.md](.github/plans/ui-polish-4-deterministic-colors.md) | Add `EntityColorService` for hash-based deterministic color assignment |
| 5 | [ui-polish-5-dashboard-stacked-chart.md](.github/plans/ui-polish-5-dashboard-stacked-chart.md) | Convert Dashboard "Usage Over Time" to stacked area chart matching Monitor/Allocations pattern |
| 6 | [ui-polish-6-chart-declutter.md](.github/plans/ui-polish-6-chart-declutter.md) | Add "top N + Others" aggregation for charts and donut with 100+ clients |

## Key Decisions

- **Custom CSS skeleton, not a library** — Radzen Blazor v10.0.5 has no built-in skeleton component. A lightweight CSS animation + simple `SkeletonBlock.razor` component keeps it dependency-free.
- **Deterministic colors via string hash** — Use a stable hash of the entity ID to index into a fixed HSL palette. This guarantees the same entity always gets the same color regardless of render order or session.
- **Top N + Others for declutter** — When client count exceeds a threshold (e.g. 10), only the top N by usage are shown individually; the rest are aggregated into a single "Others" series. This applies to stacked area charts and the donut chart.
- **Dashboard chart becomes stacked area** — The Dashboard "Usage Over Time" currently uses `RadzenLineSeries` per target/aggregate. It will be refactored to use `RadzenStackedAreaSeries` per client, matching Monitor and Allocations exactly.
- **Utilization bars in data grids are NOT loaders** — The determinate `RadzenProgressBar` inside table columns (showing % utilization) are data visualizations and are intentionally kept as-is.
