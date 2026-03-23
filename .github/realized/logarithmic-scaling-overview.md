# Plan: Logarithmic Axis Scaling for Charts

## Status: ✅ All steps completed

## Overview

All charts in the AdminUI (Dashboard, Monitor, Active Allocations) currently use a linear Y-axis, which makes small values nearly invisible when large values dominate the same graph. This plan adds a user-togglable logarithmic scale option so that ranges like 0–100 occupy the same visual space as 100–1,000 or 1,000–10,000.

The feature follows the same pattern as the existing time range and polling interval preferences: a global default in Settings, with per-chart overrides via a new "chart settings" flyout chip that replaces the current standalone `TimeRangeSelector` + `PollingIntervalSelector` chips. Since Radzen's `RadzenValueAxis` does not natively support log scale, data values will be transformed with `Math.Log10(value + 1)` before rendering, and the axis `Formatter` callback will display the original values on tick labels.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [logarithmic-scaling-1-foundation.md](logarithmic-scaling-1-foundation.md) | ✅ Add `AxisScaleType` enum, update `UserPreferences`, add `LogarithmicScaleHelper` utility |
| 2 | [logarithmic-scaling-2-settings-card.md](logarithmic-scaling-2-settings-card.md) | ✅ Add "Default Axis Scale" card to the Settings page |
| 3 | [logarithmic-scaling-3-chart-settings-component.md](logarithmic-scaling-3-chart-settings-component.md) | ✅ Create `ChartSettingsDropdown` flyout component containing time range, polling interval, and axis scale selectors |
| 4 | [logarithmic-scaling-4-chart-integration.md](logarithmic-scaling-4-chart-integration.md) | ✅ Wire up all three chart pages — replace dual chips with `ChartSettingsDropdown`, apply log transform + axis formatter |

## Key Decisions

- **Data transformation approach** — Use `Math.Log10(value + 1)` on data point values and axis cap lines before feeding them to Radzen chart series. The `+1` handles zero values cleanly. The `RadzenValueAxis.Formatter` callback converts back to display real values on tick labels.
- **Per-chart override + global default** — Same lifecycle as time range / polling interval: global default stored in `UserPreferences` and loaded by `UserPreferencesService`; each chart page holds a local `_axisScaleType` state that can be toggled independently via the settings flyout.
- **Unified chart settings flyout** — The standalone `PollingIntervalSelector` and `TimeRangeSelector` pill chips in chart headers are replaced by a single gear-icon `ChartSettingsDropdown` pill. Inside the flyout, all three options (time range, polling interval, axis scale) appear as labeled chip rows with the same dropdown behavior they have today.
- **Stacked area compatibility** — Log transform is applied to each series's `Value` property and the cap/limit `LineSeries`; the stacking still works because Radzen stacks the transformed values, which visually compresses large totals while amplifying small ones.
- **Naming** — Enum values: `Linear`, `Logarithmic`. User-facing labels: "Linear" / "Logarithmic". CSS class prefix: `cm-chart-settings-dropdown`.
