# Plan: Time Range Filtering & Settings Page

## Status: 🔲 Not started

## Overview

The AdminUI currently has no user-configurable time ranges — the Dashboard hardcodes a 1-hour window (13 × 5-min buckets) in `StatisticsService.GetUsageTimeSeriesAsync()`, the Monitor page hardcodes `now.AddHours(-1)` and `now.AddMinutes(-5)`, and each component manages its own time windows independently. The Settings button in the sidebar navigates nowhere.

This plan introduces a unified time range filtering system with sensible presets (1m through 90d), a reusable pill-style selector component matching the Metric dashboard design, a Settings page with dark/light mode toggle and default time range selection, and wires everything together across Dashboard and Monitor pages. Preferences are persisted in `localStorage` via JS interop.

**Current state**: Hardcoded time ranges per component, no Settings page, no dark mode, dead Settings link.
**Desired end state**: Pill-style time range selector on charts, Settings page at `/settings` with dark/light toggle and default range, all pages respect the user's default, CSS variable dark theme.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [time-range-settings-1-foundation.md](time-range-settings-1-foundation.md) | ✅ TimeRange model, dark theme CSS, JS interop for localStorage, UserPreferencesService |
| 2 | [time-range-settings-2-settings-page.md](time-range-settings-2-settings-page.md) | `/settings` page with dark/light toggle and default time range picker, wire sidebar link |
| 3 | [time-range-settings-3-api-updates.md](time-range-settings-3-api-updates.md) | Add `from`/`to` params to `usage-timeseries` and `client-usage-breakdown` API endpoints |
| 4 | [time-range-settings-4-selector-component.md](time-range-settings-4-selector-component.md) | Reusable `TimeRangeSelector.razor` pill-button component matching Metric design |
| 5 | [time-range-settings-5-page-integration.md](time-range-settings-5-page-integration.md) | Wire TimeRangeSelector into Dashboard and Monitor, respect default from preferences |

## Key Decisions

- **Granularity auto-mapping** — `FiveMinute` for ranges ≤6h, `Hour` for ranges ≤1d (7d inclusive), `Day` for ≥30d. The API already supports `BucketGranularity` with `FiveMinute`, `Hour`, `Day` and has rollup logic.
- **Preset list** — Minutes: 1, 5, 15, 30 | Hours: 1, 3, 6, 12 | Days: 1, 7, 30, 90. The "1 min" preset shows the most recent complete 5-minute bucket (smallest available granularity).
- **Persistence mechanism** — `localStorage` via JS interop. No authentication system exists, so server-side preferences are unnecessary. A `UserPreferencesService` (scoped, Blazor Server) reads/writes via `IJSRuntime`.
- **Dark mode approach** — CSS variable overrides on `html[data-theme="dark"]`. All existing CSS already uses CSS variables from `theme.css`, so overriding `:root` variables under the `[data-theme="dark"]` selector is sufficient. The `data-theme` attribute is set on `<html>` via JS interop on app start.
- **Time range selector style** — Pill-style grouped buttons (Minutes | Hours | Days) matching the Metric dashboard reference filter aesthetic, not dropdowns. Rendered as a horizontal row of small rounded buttons with an active highlight.
- **Component placement** — The time range selector appears in the chart header area (`.cm-dashboard__filters`) alongside existing filter dropdowns on Dashboard, and in `.cm-monitor__filters` on Monitor.
- **Default time range** — 1 hour (matches current hardcoded behavior) unless the user changes it in Settings.
