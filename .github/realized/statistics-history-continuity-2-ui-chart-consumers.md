# Plan: Restore Statistics History Continuity — Step 2: UI Chart Consumers

> **Status**: ✅ Completed
> **Prerequisite**: [statistics-history-continuity-1-storage-history.md](statistics-history-continuity-1-storage-history.md)
> **Next**: None — this is the final step.
> **Parent**: [statistics-history-continuity-overview.md](statistics-history-continuity-overview.md)

## TL;DR

Once storage returns continuous history again, make the Admin UI chart pages consume that data by timestamp instead of by preformatted labels. The main goal is to keep `/`, `/monitor`, and `/allocations` stable when fresh second-level points and rolled-up five-minute points coexist in the same selected window.

## Reference Pattern

In [../../ClientManager.AdminUI/Components/Pages/Dashboard.razor](../../ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- Use one page-owned load path for chart refreshes and let time-range, visibility, and polling callbacks all funnel through it.
- Convert raw server timestamps to `ChartBucketAggregator.RawPoint` values and rebucket them before building chart series.

In [../../ClientManager.AdminUI/Services/ChartBucketAggregator.cs](../../ClientManager.AdminUI/Services/ChartBucketAggregator.cs):
- Bucket raw UTC timestamps into a fixed visual density instead of trusting the incoming granularity to map cleanly to chart labels.
- Derive labels from the total time range, not from the original server bucket type.

In [../../ClientManager.AdminUI/Components/ChartSettingsDropdown.razor](../../ClientManager.AdminUI/Components/ChartSettingsDropdown.razor) and [../../ClientManager.AdminUI/Components/Pages/Settings.razor](../../ClientManager.AdminUI/Components/Pages/Settings.razor):
- Resolve default time range and polling preferences through `UserPreferencesService` once, then let pages own their reload behavior.
- Keep selectors thin; do not move history-repair logic into component state.

## Steps

### 1. Rebucket monitor and allocations charts from raw timestamps

Edit [../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor) and [../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor) to stop building chart series from `SortedSet<string>` and `Dictionary<string, double>` lookups keyed by formatted labels. Those structures are fragile once repaired server responses can include multiple timestamps that format to the same minute label.

Instead, keep raw `DateTime` timestamps through the fetch phase and use `ChartBucketAggregator` to produce the display buckets. If a small shared helper is warranted, place it in `ClientManager.AdminUI/Services` so the three chart pages converge on one pattern.

```csharp
var seriesPoints = entries.ToDictionary(
    entry => entry.ClientId,
    entry => history.Points.Select(point => new ChartBucketAggregator.RawPoint(point.Timestamp, point.GrantedCount)));
```

### 2. Re-evaluate short-range preset shaping and first-load refresh behavior

Review [../../ClientManager.AdminUI/Models/TimeRangePreset.cs](../../ClientManager.AdminUI/Models/TimeRangePreset.cs), [../../ClientManager.AdminUI/Components/TimeRangeSelector.razor](../../ClientManager.AdminUI/Components/TimeRangeSelector.razor), and [../../ClientManager.AdminUI/Components/Pages/Dashboard.razor](../../ClientManager.AdminUI/Components/Pages/Dashboard.razor) after the storage fix lands. Keep the existing preference-driven initialization pattern, but verify that the shortest presets still produce meaningful charts.

At minimum, re-evaluate the `5m` preset: if the repaired backend still yields an overly coarse one-point chart for that range, switch only that preset to a finer granularity such as `Second`. Do not introduce separate persistence for transient chart data; reopening the page should succeed because the server history is continuous, not because the UI cached the series locally.

### 3. Align summary views with the repaired history contract

Touch [../../ClientManager.AdminUI/Components/Pages/Dashboard.razor](../../ClientManager.AdminUI/Components/Pages/Dashboard.razor), [../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor), and [../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor) only where they still assume “recent” means a single coarse bucket or a label-derived timeseries. Keep page logic thin: the repaired storage contract should remain the source of truth, and the UI should only normalize it for chart rendering and immediate page refresh.

## Verification

- `dotnet build ClientManager.AdminUI/ClientManager.AdminUI.csproj`
- UI: Navigate to `/`, select `5m`, `15m`, and `1h`, and verify chart density stays readable and no blank tail appears after more than five minutes of live traffic.
- UI: Navigate to `/monitor`, open both a single service and `All Services`, and verify per-client series still render after a hard refresh without overwritten labels or missing recent points.
- UI: Navigate to `/allocations`, open both a single pool and `All Pools`, and verify active-slot history and denied counts remain populated across refresh and reopen.
- UI: Capture screenshots of `/`, `/monitor`, and `/allocations` showing no error banners or empty charts while the traffic generator is running.