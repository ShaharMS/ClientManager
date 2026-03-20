# Plan: Unified Monitor & Allocations — Step 2: Active Allocations Page Redesign

> **Status**: 🔲 Not started
> **Prerequisite**: [unified-monitor-allocations-1-monitor.md](unified-monitor-allocations-1-monitor.md)
> **Next**: None — this is the final step.
> **Parent**: [unified-monitor-allocations-overview.md](unified-monitor-allocations-overview.md)

## TL;DR

Redesign `ActiveAllocations.razor` to match the unified layout established in Step 1: add a client multi-select dropdown alongside the existing pool dropdown (with "All pools" option), replace the stacked bar chart with per-client stacked area charts + max-slots cap line (one chart card per pool), enhance the per-client detail table with consistent utilization bars, insert an `<hr>` separator, and move the all-pools summary table (already exists) below the separator — always unfiltered.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor) (after Step 1):
- The exact same layout structure to replicate: filter bar → chart cards → breakdown table → `<hr>` → all-targets summary
- `RadzenStackedAreaSeries` chart with per-client areas + dashed cap line
- `TargetChartData` / `ClientAreaSeries` / `ChartPoint` record shape
- All-targets summary table with `RadzenProgressBar` utilization column

In [ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor) (current):
- Timer-based auto-refresh every 10 seconds
- `_poolOptions` list built from `StatsService.GetResourcePoolStatsAsync()`
- `ClientBarSeries` / `PoolSlotPoint` / `AllocationClientRow` records
- Pool summary table with Active/Max/Available/Utilization bar/Status columns (this becomes the bottom all-targets table)

## Steps

### 1. Add client multi-select dropdown

Add a client list and selection state alongside the existing pool dropdown:

```csharp
@inject ClientApiService ClientService

private List<ClientOption> _clientOptions = [];
private IEnumerable<string>? _selectedClientIds;
```

In the filter bar, add a second dropdown after the pool dropdown:

```razor
<div class="cm-monitor__filters">
    <RadzenDropDown TValue="string?" @bind-Value="_selectedPoolId"
        Data="@_poolOptions" TextProperty="Name" ValueProperty="Id"
        Placeholder="All pools" AllowClear="true"
        Change="@(async _ => await RefreshAsync())" Style="width: 220px;" />
    <RadzenDropDown TValue="IEnumerable<string>?" @bind-Value="_selectedClientIds"
        Multiple="true" Data="@_clientOptions" TextProperty="Name" ValueProperty="Id"
        Placeholder="All clients" Style="width: 200px;"
        Change="@(async _ => await RefreshAsync())" />
    <span class="cm-badge cm-badge--success" style="margin-left: auto;">
        <RadzenIcon Icon="fiber_manual_record" Style="font-size: 10px;" /> Auto-refreshing
    </span>
</div>
```

Load clients in `OnInitializedAsync`:

```csharp
var clients = await ClientService.GetAllAsync();
_clientOptions = clients.Select(c => new ClientOption(c.Id, c.Name)).ToList();
```

### 2. Replace stacked bar chart with stacked area charts

Remove the existing `_clientSlotSeries` bar chart section. Replace with chart cards that loop over visible pools, matching Monitor's layout:

```razor
@foreach (var targetChart in _targetCharts)
{
    <div class="cm-monitor__chart-card">
        <div class="cm-dashboard__chart-header">
            <span class="cm-dashboard__chart-title">@targetChart.TargetName — Slot Usage</span>
        </div>
        <RadzenChart Style="height: 300px;">
            @foreach (var clientArea in targetChart.ClientSeries)
            {
                <RadzenStackedAreaSeries Data="@clientArea.Points"
                    CategoryProperty="Label" ValueProperty="Value"
                    Title="@clientArea.ClientName" />
            }
            <RadzenLineSeries Data="@targetChart.CapSeries"
                CategoryProperty="Label" ValueProperty="Value"
                Title="Max Slots" LineType="LineType.Dashed"
                Stroke="var(--color-text-secondary)" />
            <RadzenCategoryAxis />
            <RadzenValueAxis />
            <RadzenLegend Position="LegendPosition.Bottom" />
        </RadzenChart>
    </div>
}
```

### 3. Build per-client time-series data in RefreshAsync

Replace the `clientSeriesMap` logic with per-client historical data for stacked area charts:

```csharp
private List<TargetChartData> _targetCharts = [];

// Determine visible pools
var visiblePools = _selectedPoolId is null
    ? _pools
    : _pools.Where(p => p.ResourcePoolId == _selectedPoolId).ToList();

var now = DateTime.UtcNow;
var from = now.AddHours(-1);
var charts = new List<TargetChartData>();

foreach (var pool in visiblePools)
{
    var breakdown = await StatsService.GetClientUsageBreakdownAsync(
        "ResourcePool", pool.ResourcePoolId, _selectedClientIds);

    var clientAreas = new List<ClientAreaSeries>();
    foreach (var entry in breakdown?.Entries ?? [])
    {
        var history = await StatsService.GetHistoricalUsageAsync(
            "ResourcePool", pool.ResourcePoolId, entry.ClientId,
            from, now, "FiveMinute");
        clientAreas.Add(new ClientAreaSeries(entry.ClientName,
            history?.Points.Select(p =>
                new ChartPoint(p.Timestamp.ToLocalTime().ToString("HH:mm"),
                    (double)p.GrantedCount)).ToList() ?? []));
    }

    // Cap line = pool's MaxSlots
    var capPoints = clientAreas.FirstOrDefault()?.Points
        .Select(p => new ChartPoint(p.Label, pool.MaxSlots)).ToList()
        ?? [];

    charts.Add(new TargetChartData(pool.Name, clientAreas, capPoints));
}
_targetCharts = charts;
```

### 4. Restructure the Client Allocation Detail table

Keep the existing client detail table but ensure it matches Monitor's pattern exactly — with consistent utilization bar and positioning below the charts:

```razor
<div class="cm-list-page__table-card" style="margin-top: var(--space-lg);">
    <div class="cm-dashboard__chart-header">
        <span class="cm-dashboard__chart-title">Client Allocation Detail</span>
    </div>
    <RadzenDataGrid Data="@_clientDetailRows" TItem="AllocationClientRow" ...>
        <!-- Same columns as current, with utilization bar (already exists) -->
    </RadzenDataGrid>
</div>
```

Filter `_clientDetailRows` by `_selectedClientIds` when clients are selected.

### 5. Add `<hr>` separator and position all-pools summary table below it

Move the existing pool summary table (`_filteredPools` grid with Active/Max/Available/Utilization/Status) below an `<hr>` separator. **Remove all dropdown filtering from this table** — it always shows all pools:

```razor
<hr class="cm-monitor__separator" />

<div class="cm-list-page__table-card" style="margin-top: var(--space-lg);">
    <div class="cm-dashboard__chart-header">
        <span class="cm-dashboard__chart-title">All Resource Pools</span>
    </div>
    <RadzenDataGrid Data="@_pools" TItem="ResourcePoolStatistics" ...>
        <Columns>
            <RadzenDataGridColumn Property="Name" Title="Pool" />
            <RadzenDataGridColumn Property="ActiveAllocations" Title="Active" Width="100px" />
            <RadzenDataGridColumn Property="MaxSlots" Title="Max" Width="100px" />
            <RadzenDataGridColumn Property="AvailableSlots" Title="Available" Width="100px" />
            <RadzenDataGridColumn Title="Utilization" Width="200px">
                <Template Context="pool">
                    @{
                        var pct = pool.MaxSlots > 0
                            ? (int)(pool.ActiveAllocations * 100.0 / pool.MaxSlots) : 0;
                        var barStyle = pct >= 100 ? ProgressBarStyle.Danger
                            : pct >= 75 ? ProgressBarStyle.Warning : ProgressBarStyle.Success;
                    }
                    <RadzenProgressBar Value="@pct" Max="100" ShowValue="true"
                        ProgressBarStyle="@barStyle" Style="height: 20px;" />
                </Template>
            </RadzenDataGridColumn>
            <RadzenDataGridColumn Title="Status" Width="110px" Sortable="false">
                <!-- existing badge template -->
            </RadzenDataGridColumn>
        </Columns>
    </RadzenDataGrid>
</div>
```

Note: the data source changes from `_filteredPools` to `_pools` (unfiltered). Remove the `_filteredPools` computed property — no longer needed.

### 6. Add record types matching Monitor's pattern

```csharp
private record ClientOption(string Id, string Name);
private record TargetChartData(string TargetName, List<ClientAreaSeries> ClientSeries, List<ChartPoint> CapSeries);
private record ClientAreaSeries(string ClientName, List<ChartPoint> Points);
private record ChartPoint(string Label, double Value);
```

Remove the old `ClientBarSeries` and `PoolSlotPoint` records that powered the bar chart.

### 7. Clean up unused state

Remove `_clientSlotSeries` field and `ClientBarSeries`/`PoolSlotPoint` records.  
Remove `_filteredPools` computed property (bottom table now always uses `_pools`).

## Verification

- Project compiles without errors (`dotnet build ClientManager.AdminUI`)
- **UI: Navigate to `/allocations` — verify two dropdowns appear: pool selector (with "All pools" as default) and client multi-select**
- **UI: Verify stacked area chart(s) render with per-client colored filled areas and a dashed "Max Slots" cap line**
- **UI: Select a specific pool — verify only one chart card appears for that pool**
- **UI: Select specific client(s) — verify charts and detail table filter to those clients**
- **UI: Verify the Client Allocation Detail table has a utilization bar column (already existed, confirm it's still there)**
- **UI: Verify an `<hr>` separator is visible below the detail table**
- **UI: Verify the "All Resource Pools" summary table appears at the bottom and always shows all pools regardless of dropdown selection**
- **UI: Verify the all-pools table has 0–100% utilization progress bars**
- **UI: Compare layout side-by-side with `/monitor` — both pages should have the same structural layout**
- **UI: Take a screenshot to confirm no layout breakage or error banners**
