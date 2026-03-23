# Plan: Logarithmic Axis Scaling — Step 4: Chart Page Integration

> **Status**: ✅ Completed
> **Prerequisite**: [logarithmic-scaling-3-chart-settings-component.md](logarithmic-scaling-3-chart-settings-component.md)
> **Next**: None — this is the final step.
> **Parent**: [logarithmic-scaling-overview.md](logarithmic-scaling-overview.md)

## TL;DR

Replace the standalone `PollingIntervalSelector` + `TimeRangeSelector` chips in all three chart pages (Dashboard, Monitor, Active Allocations) with the new `ChartSettingsDropdown`. Wire up the logarithmic transform: when `AxisScaleType.Logarithmic` is active, transform data values with `LogarithmicScaleHelper.Transform()` before feeding them to chart series, and set `RadzenValueAxis.Formatter` to `LogarithmicScaleHelper.FormatAxisLabel` so tick labels show real values.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- Chart header layout: left filters div (`cm-dashboard__filters`) + right toolbar div with selectors
- `RadzenStackedAreaSeries` iterates over `targetChart.ClientSeries`, each with `ValueProperty="Value"`
- `RadzenLineSeries` for cap line with same structure
- `<RadzenValueAxis />` currently has no Formatter set
- `_timeRange` state field, `OnTimeRangeChanged` / `OnPollingIntervalChanged` handlers

The same pattern repeats in [Monitor.razor](ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor) and [ActiveAllocations.razor](ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor).

## Steps

### 1. Add `_axisScaleType` state + handler to Dashboard.razor

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](ClientManager.AdminUI/Components/Pages/Dashboard.razor) `@code` block, add:

```csharp
private AxisScaleType _axisScaleType = AxisScaleType.Linear;

private Task OnAxisScaleChanged(AxisScaleType scale)
{
    _axisScaleType = scale;
    StateHasChanged();
    return Task.CompletedTask;
}
```

### 2. Replace the selector chips in Dashboard header

Replace:
```razor
<div style="display: flex; gap: var(--space-sm); align-items: center;">
    <PollingIntervalSelector OnIntervalChanged="OnPollingIntervalChanged" />
    <TimeRangeSelector OnRangeChanged="OnTimeRangeChanged" />
</div>
```

With:
```razor
<ChartSettingsDropdown OnTimeRangeChanged="OnTimeRangeChanged"
                       OnPollingIntervalChanged="OnPollingIntervalChanged"
                       OnAxisScaleChanged="OnAxisScaleChanged" />
```

### 3. Create a helper method for transforming chart data

Add a private method (or use inline logic) that conditionally transforms point values:

```csharp
private double ScaleValue(double value) =>
    _axisScaleType == AxisScaleType.Logarithmic
        ? LogarithmicScaleHelper.Transform(value)
        : value;
```

### 4. Apply transform to chart series data

When building `RadzenStackedAreaSeries` and `RadzenLineSeries`, the `Data` binding must use transformed points. Create transformed copies of the chart data when the scale type changes. 

One clean approach: instead of transforming at data-build time, create a display-projection in the Razor markup. Wrap each series's data through a helper that produces transformed copies:

```csharp
private List<TimeSeriesPoint> TransformPoints(List<TimeSeriesPoint> points) =>
    _axisScaleType == AxisScaleType.Logarithmic
        ? points.Select(p => p with { Value = LogarithmicScaleHelper.Transform(p.Value) }).ToList()
        : points;
```

Then in the Razor:
```razor
<RadzenStackedAreaSeries Data="@TransformPoints(clientArea.Points)" ... />
<RadzenLineSeries Data="@TransformPoints(targetChart.CapSeries)" ... />
```

### 5. Set axis Formatter

Update `<RadzenValueAxis />` to conditionally use the log formatter:

```razor
<RadzenValueAxis Formatter="@(_axisScaleType == AxisScaleType.Logarithmic ? LogarithmicScaleHelper.FormatAxisLabel : null)" />
```

### 6. Repeat for Monitor.razor

Apply the exact same changes to [ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor):
- Add `_axisScaleType` field + `OnAxisScaleChanged` handler
- Replace `PollingIntervalSelector` + `TimeRangeSelector` with `ChartSettingsDropdown`
- Add `ScaleValue` / `TransformPoints` helper
- Apply transform to series data
- Set `RadzenValueAxis.Formatter`

Note: Monitor uses `ChartPoint` record (not `TimeSeriesPoint`). The `TransformPoints` method should work with `ChartPoint`:
```csharp
private List<ChartPoint> TransformPoints(List<ChartPoint> points) =>
    _axisScaleType == AxisScaleType.Logarithmic
        ? points.Select(p => p with { Value = LogarithmicScaleHelper.Transform(p.Value) }).ToList()
        : points;
```

### 7. Repeat for ActiveAllocations.razor

Apply the exact same changes to [ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor):
- Same pattern as Monitor — uses `ChartPoint` record
- Replace chips, add state, transform data, set formatter

### 8. Remove unused standalone selector imports (if needed)

If `TimeRangeSelector` and `PollingIntervalSelector` are no longer used by any page after this change, they can be left in place (the `ChartSettingsDropdown` may reference them internally, or they may still be useful). Do NOT delete them — they're still valid components.

## Verification

- `dotnet build ClientManager.AdminUI/ClientManager.AdminUI.csproj` compiles without errors
- **UI: Navigate to Dashboard (`/`) — verify the chart header shows a single "Settings" pill instead of two separate chips**
- **UI: Click the Settings pill — verify the flyout opens with three sections: Time Range, Refresh Rate, Vertical Axis**
- **UI: Select different time ranges in the flyout — verify chart data updates (same behavior as before)**
- **UI: Select different polling intervals — verify refresh rate changes**
- **UI: Click "Logarithmic" under Vertical Axis — verify the Y-axis labels change to logarithmic formatting (e.g., "1", "10", "100", "1K") and small data series become visually larger relative to dominant ones**
- **UI: Switch back to "Linear" — verify the chart returns to normal linear scale**
- **UI: Navigate to Monitor (`/monitor`) — repeat the same checks: Settings pill, flyout, all three options work**
- **UI: Navigate to Active Allocations (`/allocations`) — repeat the same checks**
- **UI: Take screenshots of a chart in both Linear and Logarithmic modes to confirm visual difference**
- **UI: Verify no layout breakage — the flyout should not overflow the viewport, chips should wrap correctly**
