# Plan: Dashboard Drilldowns — Step 3: Active Allocations Redesign

> **Status**: ✅ Completed
> **Prerequisite**: [dashboard-drilldowns-2-monitor-page.md](dashboard-drilldowns-2-monitor-page.md)
> **Next**: None — this is the final step.
> **Parent**: [dashboard-drilldowns-overview.md](dashboard-drilldowns-overview.md)

## TL;DR

Enhance the existing Active Allocations page with per-client per-pool real-time breakdowns — a stacked bar chart of slot utilization by client, and a detail table showing each client's active allocations, slot limits, denied attempts, and capacity status. The page auto-refreshes every 10 seconds (preserving existing behavior) and emphasizes real-time state over historical trends, matching the nature of resource pool slots.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor):
- Route `/allocations`, injects `StatisticsApiService`, auto-refreshes via `Timer`
- Uses `RadzenDataGrid` with `RadzenProgressBar` for utilization
- `IDisposable` for timer cleanup
- Currently shows per-pool summary only (no per-client drill-down)

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- Filter dropdowns, chart + table layout
- `RadzenChart` with `RadzenBarSeries` available for stacked bars

In [ClientManager.AdminUI/Services/StatisticsApiService.cs](ClientManager.AdminUI/Services/StatisticsApiService.cs):
- `GetResourcePoolStatsAsync()` — per-pool summary
- `GetClientUsageBreakdownAsync("ResourcePool", poolId, ...)` — per-client slot counts
- `GetHistoricalUsageAsync(...)` — historical granted/denied for resource pool acquisitions

## Steps

### 1. Redesign the page layout

Edit [ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor):

The page becomes a two-section layout:

```
┌──────────────────────────────────────────────────────────┐
│ Active Allocations                                        │
│ Real-time resource pool utilization — refreshes every 10s │
├──────────────────────────┬───────────────────────────────┤
│ Pool filter: [All ▼]    │  Auto-refresh: ● Active       │
├──────────────────────────┴───────────────────────────────┤
│                                                           │
│  ┌── Pool Utilization Overview (existing table) ────────┐│
│  │ Pool │ Active │ Max │ Available │ Utilization │ Status││
│  └──────────────────────────────────────────────────────┘│
│                                                           │
│  ┌── Per-Client Breakdown (stacked bar chart) ──────────┐│
│  │  X-axis: resource pools                               ││
│  │  Y-axis: slots                                        ││
│  │  Stacked by client (each client = a color)            ││
│  │  Max line showing pool capacity                       ││
│  └──────────────────────────────────────────────────────┘│
│                                                           │
│  ┌── Client Detail Table ───────────────────────────────┐│
│  │ Client │ Pool │ Active Slots │ Max Slots │ Denied    ││
│  │        │      │ Utilization  │           │ (5m) │ St ││
│  └──────────────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────┘
```

### 2. Add pool filter dropdown

At the top of the page, add a filter to select a specific pool or view all:

```razor
<div class="cm-monitor__filters">
    <RadzenDropDown @bind-Value="_selectedPoolId"
        Data="@_poolOptions" TextProperty="Name" ValueProperty="Id"
        Placeholder="All pools" AllowClear="true"
        Change="@(async _ => await RefreshAsync())" Style="width: 220px;" />
    <span class="cm-badge cm-badge--success" style="margin-left: auto;">
        <RadzenIcon Icon="fiber_manual_record" Style="font-size: 10px;" /> Auto-refreshing
    </span>
</div>
```

### 3. Keep the existing pool utilization table

The existing `RadzenDataGrid` with Pool / Active / Max / Available / Utilization / Status columns stays as-is. If a pool filter is selected, filter to that pool only.

### 4. Add stacked bar chart for per-client slot usage

Below the pool table:

```razor
<div class="cm-monitor__chart-card">
    <div class="cm-dashboard__chart-header">
        <span class="cm-dashboard__chart-title">Slot Usage by Client</span>
    </div>
    <RadzenChart Style="height: 300px;">
        @foreach (var clientSeries in _clientSlotSeries)
        {
            <RadzenBarSeries Data="@clientSeries.Data" CategoryProperty="PoolName"
                ValueProperty="Slots" Title="@clientSeries.ClientName" />
        }
        <RadzenCategoryAxis />
        <RadzenValueAxis />
        <RadzenLegend Position="LegendPosition.Bottom" />
    </RadzenChart>
</div>
```

**Data loading for chart**: For each pool (or the filtered pool), call `GetClientUsageBreakdownAsync("ResourcePool", poolId, null)` and group by client. Build one series per client.

### 5. Add client detail table

Below the chart:

```razor
<div class="cm-list-page__table-card">
    <div class="cm-dashboard__chart-header">
        <span class="cm-dashboard__chart-title">Client Allocation Detail</span>
    </div>
    <RadzenDataGrid Data="@_clientDetailRows" TItem="AllocationClientRow"
        AllowSorting="true" AllowPaging="true" PageSize="15"
        PagerHorizontalAlign="HorizontalAlign.Center">
        <Columns>
            <RadzenDataGridColumn TItem="AllocationClientRow" Property="ClientName" Title="Client" />
            <RadzenDataGridColumn TItem="AllocationClientRow" Property="PoolName" Title="Pool" />
            <RadzenDataGridColumn TItem="AllocationClientRow" Property="ActiveSlots" Title="Active" Width="90px" />
            <RadzenDataGridColumn TItem="AllocationClientRow" Property="MaxSlots" Title="Max" Width="80px" />
            <RadzenDataGridColumn TItem="AllocationClientRow" Title="Utilization" Width="180px">
                <Template Context="row">
                    @{
                        var pct = row.MaxSlots > 0 ? (int)(row.ActiveSlots * 100.0 / row.MaxSlots) : 0;
                        var style = pct >= 100 ? ProgressBarStyle.Danger : pct >= 75 ? ProgressBarStyle.Warning : ProgressBarStyle.Success;
                    }
                    <RadzenProgressBar Value="@pct" Max="100" ShowValue="true"
                        ProgressBarStyle="@style" Style="height: 18px;" />
                </Template>
            </RadzenDataGridColumn>
            <RadzenDataGridColumn TItem="AllocationClientRow" Property="DeniedLast5Min" Title="Denied (5m)" Width="110px" />
            <RadzenDataGridColumn TItem="AllocationClientRow" Title="Status" Width="120px" Sortable="false">
                <Template Context="row">
                    @if (row.ActiveSlots >= row.MaxSlots)
                    {
                        <span class="cm-badge" style="background: #fee2e2; color: #991b1b;">At Capacity</span>
                    }
                    else if (row.DeniedLast5Min > 0)
                    {
                        <span class="cm-badge" style="background: #fef3c7; color: #92400e;">Contention</span>
                    }
                    else
                    {
                        <span class="cm-badge cm-badge--success">Available</span>
                    }
                </Template>
            </RadzenDataGridColumn>
        </Columns>
    </RadzenDataGrid>
</div>
```

### 6. Update data loading in `RefreshAsync`

Replace the existing `RefreshAsync` with an expanded version that loads:

1. **Pool stats** (existing): `StatsService.GetResourcePoolStatsAsync()`
2. **Per-client breakdowns per pool**: for each pool (or the filtered pool), call `StatsService.GetClientUsageBreakdownAsync("ResourcePool", pool.ResourcePoolId, null)`
3. **Recent denied counts**: for each client-pool pair, call `StatsService.GetHistoricalUsageAsync("ResourcePool", poolId, clientId, fiveMinutesAgo, now, "FiveMinute")` to get denied counts

Build `_clientSlotSeries` (for the chart) and `_clientDetailRows` (for the table) from the combined data.

To avoid N+1 calls that slow down the 10-second refresh: batch-load breakdown for all pools first, then only call historical-usage for client-pool pairs that have active slots. Cache results that haven't changed between refreshes.

### 7. Add inner records

```csharp
private record PoolOption(string Id, string Name);
private record ClientBarSeries(string ClientName, List<PoolSlotPoint> Data);
private record PoolSlotPoint(string PoolName, int Slots);
private record AllocationClientRow(
    string ClientId, string ClientName, string PoolName,
    int ActiveSlots, int MaxSlots, long DeniedLast5Min);
```

### 8. Add additional service injections

The page currently only injects `StatisticsApiService`. Add:

```razor
@inject ClientApiService ClientService
@inject ResourcePoolApiService PoolService
```

These are needed to get client names and pool names for the chart series and table rows.

## Verification

- Solution compiles without errors
- The Active Allocations page shows the existing pool utilization table at the top
- Below it, a stacked bar chart shows per-client slot usage across pools
- Below the chart, a detail table shows each client's allocation state per pool with utilization bars
- The "Denied (5m)" column shows recent denied acquisition attempts from historical data
- Status badges correctly reflect: "At Capacity" (active ≥ max), "Contention" (denials but not full), "Available" (healthy)
- The pool filter dropdown narrows all three sections to a single pool
- The page auto-refreshes every 10 seconds without flicker
- Clicking the "Pool Acquisition" stat card on the dashboard still navigates here correctly
- The page handles the case where no historical data exists yet (denied column shows 0)
