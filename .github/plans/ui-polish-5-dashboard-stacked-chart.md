# Plan: UI Polish — Step 5: Dashboard Stacked Area Chart

> **Status**: 🔲 Not started
> **Prerequisite**: [ui-polish-4-deterministic-colors.md](ui-polish-4-deterministic-colors.md)
> **Next**: [ui-polish-6-chart-declutter.md](ui-polish-6-chart-declutter.md)
> **Parent**: [ui-polish-overview.md](ui-polish-overview.md)

## TL;DR

Convert the Dashboard "Usage Over Time" chart from `RadzenLineSeries` (one line per target or aggregate) to `RadzenStackedAreaSeries` (one area per client), matching the chart pattern already used in Monitor and ActiveAllocations. Keep the dashed "Cap" line as a `RadzenLineSeries` overlay. Use deterministic colors from `EntityColorService`.

## Reference Pattern

The target pattern is in [ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor) lines 35–58:

```razor
@foreach (var clientArea in targetChart.ClientSeries)
{
    <RadzenStackedAreaSeries Data="@clientArea.Points"
        CategoryProperty="Label" ValueProperty="Value"
        Title="@clientArea.ClientName" />
}
<RadzenLineSeries Data="@targetChart.CapSeries"
    CategoryProperty="Label" ValueProperty="Value"
    Title="Cap" LineType="LineType.Dashed"
    Stroke="var(--color-text-secondary)" />
```

The Dashboard currently uses:
```razor
@foreach (var series in _chartSeries)
{
    <RadzenLineSeries Data="@series.Points" CategoryProperty="Label"
        ValueProperty="Value" Title="@series.Name"
        LineType="@(series.IsDashed ? LineType.Dashed : LineType.Solid)" />
}
```

## Steps

### 1. Add per-client chart data model to Dashboard

In `ClientManager.AdminUI/Components/Pages/Dashboard.razor`, in the `@code` block, add records matching Monitor's pattern:

```csharp
private record ClientAreaSeries(string ClientId, string ClientName, List<TimeSeriesPoint> Points);
private record TargetChartData(string TargetName, List<ClientAreaSeries> ClientSeries, List<TimeSeriesPoint> CapSeries);
```

Add a field:
```csharp
private List<TargetChartData> _targetCharts = new();
```

### 2. Refactor `LoadSingleTargetChartDataAsync`

Currently this method fetches a single aggregated `UsageTimeSeries`. Refactor it to instead:

1. Fetch the `ClientUsageBreakdown` for the selected target (already done for the donut).
2. For each client in the breakdown, fetch `GetHistoricalUsageAsync` (same as Monitor does).
3. Build `ClientAreaSeries` per client with `TimeSeriesPoint` data.
4. Build a cap series from the rate limit/quota.
5. Store as `_targetCharts`.

Follow the exact data-fetching pattern from Monitor's `LoadDataAsync()` — the per-service branch (when a specific service is selected, not "All").

### 3. Refactor `LoadAllTargetsChartDataAsync`

Currently builds one `ChartSeries` per target. Refactor to:

1. For each target, fetch client breakdown + per-client historical usage.
2. Build a `TargetChartData` per target with per-client `ClientAreaSeries`.
3. Store all in `_targetCharts`.

When there are many targets and the view is "All", consider whether to show one chart per target (like Monitor does per-service) or aggregate. Follow Monitor's pattern: when "All Services" is selected, Monitor aggregates into a single chart with a "Total" series. The Dashboard should do the same for consistency.

### 4. Update the chart rendering markup

Replace the current `RadzenLineSeries` loop:

```razor
<RadzenChart Style="height: 300px;">
    @foreach (var series in _chartSeries)
    {
        <RadzenLineSeries Data="@series.Points" CategoryProperty="Label"
            ValueProperty="Value" Title="@series.Name"
            LineType="@(series.IsDashed ? LineType.Dashed : LineType.Solid)" />
    }
    <RadzenCategoryAxis />
    <RadzenValueAxis />
    <RadzenLegend Position="LegendPosition.Bottom" />
</RadzenChart>
```

With:

```razor
<RadzenChart Style="height: 300px;">
    @foreach (var targetChart in _targetCharts)
    {
        @foreach (var clientArea in targetChart.ClientSeries)
        {
            <RadzenStackedAreaSeries Data="@clientArea.Points"
                CategoryProperty="Label" ValueProperty="Value"
                Title="@clientArea.ClientName"
                Fill="@Colors.GetColor(clientArea.ClientId)"
                Stroke="@Colors.GetColor(clientArea.ClientId)" />
        }
        <RadzenLineSeries Data="@targetChart.CapSeries"
            CategoryProperty="Label" ValueProperty="Value"
            Title="Cap" LineType="LineType.Dashed"
            Stroke="var(--color-text-secondary)" />
    }
    <RadzenCategoryAxis />
    <RadzenValueAxis />
    <RadzenLegend Position="LegendPosition.Bottom" />
</RadzenChart>
```

### 5. Remove unused models

Remove the old `ChartSeries` record and `_chartSeries` field once all references are updated.

## Verification

- Project compiles without errors
- **UI: Navigate to `/` (Dashboard) — verify the "Usage Over Time" chart now shows stacked colored areas per client, with a dashed cap line, instead of plain lines**
- **UI: Change the filter dropdown to a specific service — verify per-client stacked areas appear for that service's clients**
- **UI: Change to "All Services" — verify the aggregated view shows a single "Total" area with a dashed cap line**
- **UI: Switch filter to "Resource Pool" — verify the same stacked-area behavior applies**
- **UI: Compare colors on Dashboard chart to `/monitor` — verify the same client has the same color on both pages**
