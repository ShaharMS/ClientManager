# Plan: Time Range Filtering & Settings — Step 5: Page Integration

> **Status**: 🔲 Not started
> **Prerequisite**: [time-range-settings-4-selector-component.md](time-range-settings-4-selector-component.md)
> **Next**: None — this is the final step.
> **Parent**: [time-range-settings-overview.md](time-range-settings-overview.md)

## TL;DR

Wire the `TimeRangeSelector` component into the Dashboard and Monitor pages, passing `from`/`to`/`granularity` to all API calls. Both pages respect the user's default time range preference. Time axis labels adapt based on granularity (e.g., `HH:mm` for FiveMinute, `MMM dd` for Day). The Monitor page's auto-refresh timer continues to work with the selected range.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](../../ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- Chart data loaded in `LoadChartDataAsync()` which calls `StatsService.GetUsageTimeSeriesAsync()` and `StatsService.GetClientUsageBreakdownAsync()`
- Filter dropdowns trigger `OnFilterChanged()` which calls `LoadChartDataAsync()`
- Time labels currently use `p.Timestamp.ToLocalTime().ToString("HH:mm")`

In [ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor):
- `LoadDataAsync()` hardcodes `now.AddHours(-1)` and `now.AddMinutes(-5)` for all data fetches
- Uses `StatsService.GetHistoricalUsageAsync()` which already accepts `from`/`to`/`granularity`
- Auto-refreshes every 10 seconds via `Timer`

## Steps

### 1. Add time label formatting helper

Add a static method to `TimeRangePreset` (in `ClientManager.AdminUI/Models/TimeRangePreset.cs`) or create a small helper in the same file:

```csharp
public string FormatTimestamp(DateTime timestamp)
{
    var local = timestamp.ToLocalTime();
    return Granularity switch
    {
        "Day" => local.ToString("MMM dd"),
        "Hour" => local.ToString("MMM dd HH:mm"),
        _ => local.ToString("HH:mm")
    };
}
```

### 2. Integrate TimeRangeSelector into Dashboard

In `ClientManager.AdminUI/Components/Pages/Dashboard.razor`:

**Add a `_timeRange` field** in the `@code` block:
```csharp
private TimeRangePreset _timeRange = TimeRangePreset.Default;
```

**Place the TimeRangeSelector** in the "Usage Over Time" chart header, before the existing dropdowns:

```razor
<div class="cm-dashboard__chart-header">
    <span class="cm-dashboard__chart-title">Usage Over Time</span>
    <div class="cm-dashboard__filters">
        <TimeRangeSelector OnRangeChanged="OnTimeRangeChanged" />
        <RadzenDropDown @bind-Value="_selectedFilterType" ... />
        <RadzenDropDown @bind-Value="_selectedTargetId" ... />
        <RadzenDropDown @bind-Value="_selectedClientIds" ... />
    </div>
</div>
```

**Add `OnTimeRangeChanged` handler**:
```csharp
private async Task OnTimeRangeChanged(TimeRangePreset preset)
{
    _timeRange = preset;
    await LoadChartDataAsync();
}
```

**Update `LoadSingleTargetChartDataAsync`** to pass time range:
```csharp
private async Task LoadSingleTargetChartDataAsync()
{
    var from = _timeRange.GetFrom();
    var to = _timeRange.GetTo();
    var granularity = _timeRange.Granularity;

    var timeSeries = await StatsService.GetUsageTimeSeriesAsync(
        _selectedFilterType, _selectedTargetId!, _selectedClientIds,
        from, to, granularity);
    if (timeSeries is null) return;

    _chartSeries.Add(new ChartSeries("Usage", timeSeries.UsagePoints
        .Select(p => new TimeSeriesPoint(_timeRange.FormatTimestamp(p.Timestamp), p.Value))
        .ToList(), false));
    _chartSeries.Add(new ChartSeries("Limit", timeSeries.CapPoints
        .Select(p => new TimeSeriesPoint(_timeRange.FormatTimestamp(p.Timestamp), p.Value))
        .ToList(), true));
}
```

**Update `LoadAllTargetsChartDataAsync`** similarly — pass `from`/`to`/`granularity` and use `_timeRange.FormatTimestamp()`.

**Update donut chart calls** — pass `from`/`to`/`granularity` to `GetClientUsageBreakdownAsync`:
```csharp
var breakdown = await StatsService.GetClientUsageBreakdownAsync(
    _selectedFilterType, target.Id, _selectedClientIds,
    _timeRange.GetFrom(), _timeRange.GetTo(), _timeRange.Granularity);
```

**Remove the hardcoded first load** — currently `OnInitializedAsync` triggers `LoadChartDataAsync()` directly. Instead, the `TimeRangeSelector`'s `OnAfterRenderAsync` will emit its initial value which triggers `OnTimeRangeChanged`, so the first load happens automatically. Remove the explicit `LoadChartDataAsync()` call in `OnInitializedAsync` (after setting up filter targets) since it would fire before the time range is set. Guard `LoadChartDataAsync` to no-op if `_timeRange` hasn't been set yet, or simply let the TimeRangeSelector drive the first load.

### 3. Integrate TimeRangeSelector into Monitor

In `ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor`:

**Add a `_timeRange` field**:
```csharp
private TimeRangePreset _timeRange = TimeRangePreset.Default;
```

**Place the TimeRangeSelector** in `.cm-monitor__filters`:
```razor
<div class="cm-monitor__filters">
    <TimeRangeSelector OnRangeChanged="OnTimeRangeChanged" />
    <RadzenDropDown TValue="string" @bind-Value="_selectedServiceId" ... />
    <RadzenDropDown @bind-Value="_selectedClientIds" ... />
</div>
```

**Add `OnTimeRangeChanged` handler**:
```csharp
private async Task OnTimeRangeChanged(TimeRangePreset preset)
{
    _timeRange = preset;
    await LoadDataAsync();
    StateHasChanged();
}
```

**Update `LoadDataAsync`** to use `_timeRange` instead of hardcoded values:
```csharp
private async Task LoadDataAsync()
{
    var from = _timeRange.GetFrom();
    var to = _timeRange.GetTo();
    var granularity = _timeRange.Granularity;

    // Replace: var from = now.AddHours(-1);
    // Replace: var recentFrom = now.AddMinutes(-5);
    // Use the selected time range for chart data.
    // For the "recent" rows (GrantedLast5Min columns), still use last 5 min.
    var recentFrom = DateTime.UtcNow.AddMinutes(-5);
    var recentTo = DateTime.UtcNow;

    // ... rest of LoadDataAsync, passing from/to/granularity to
    // StatsService.GetHistoricalUsageAsync calls for charts,
    // and recentFrom/recentTo for the table rows.
}
```

**Update timestamp labels** in `LoadDataAsync` to use `_timeRange.FormatTimestamp()` instead of hardcoded `"HH:mm"`:
```csharp
var label = _timeRange.FormatTimestamp(point.Timestamp);
```

### 4. Ensure Timer respects current time range

In the Monitor's timer callback, `LoadDataAsync` already re-calculates `from`/`to` from `_timeRange.GetFrom()`/`GetTo()` each time, so the window slides forward automatically on each 10-second refresh. No additional changes needed.

### 5. Handle initial load ordering

Both pages need to handle the fact that `OnInitializedAsync` fires before `OnAfterRenderAsync` (where the TimeRangeSelector emits its initial value). Two approaches:

**Option A (preferred)**: Don't load chart data in `OnInitializedAsync`. Only load non-chart data (overview, services list, clients list). The TimeRangeSelector's `OnAfterRenderAsync` fires the initial `OnRangeChanged`, which triggers chart loading.

**Option B**: Load chart data in `OnInitializedAsync` with `TimeRangePreset.Default`, then reload when the TimeRangeSelector emits.

Use **Option A** for both pages to avoid a double-load.

## Verification

- Both projects compile without errors.
- **UI: Navigate to Dashboard at `/` — the time range selector pills appear above the chart filter dropdowns.**
- **UI: Click "5m" — the Usage Over Time chart reloads showing only the last 5 minutes of data. The donut chart also updates.**
- **UI: Click "7d" — the chart x-axis shows date labels ("Mar 14", "Mar 15" etc.) instead of time labels.**
- **UI: Click "90d" — the chart shows daily granularity spanning ~3 months.**
- **UI: Go to Settings, set default time range to "30m". Navigate back to Dashboard — the "30m" pill is pre-selected.**
- **UI: Navigate to Monitor at `/monitor` — the time range selector appears alongside service/client filters.**
- **UI: Select "3h" on Monitor — the stacked area chart shows 3 hours of data. The table still shows "Last 5m" data for Req/Denied columns.**
- **UI: Wait 10 seconds on Monitor — the chart auto-refreshes with the same time range, sliding the window forward.**
- **UI: Toggle dark mode in Settings, then visit Dashboard and Monitor — verify charts and pills render correctly in dark mode.**
- **UI: Take screenshots of Dashboard and Monitor with different time ranges selected.**
