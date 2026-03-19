# Plan: Dashboard Drilldowns — Step 2: Monitor Page

> **Status**: 🔲 Not started
> **Prerequisite**: [dashboard-drilldowns-1-card-arrows.md](dashboard-drilldowns-1-card-arrows.md)
> **Next**: [dashboard-drilldowns-3-allocations-redesign.md](dashboard-drilldowns-3-allocations-redesign.md)
> **Parent**: [dashboard-drilldowns-overview.md](dashboard-drilldowns-overview.md)

## TL;DR

Create a new **Monitor** page (`/monitor`) that provides per-client per-service analytics of request traffic. Includes a time-series chart showing requests over time with rate limit cap lines and denied request overlay, plus a detail table showing each client-service pair's current rate, cap, remaining budget, and denial count. Auto-refreshes for near-real-time visibility. Also adds a "Monitor" nav item to the sidebar.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- Filter dropdowns for service/client selection
- `RadzenChart` with `RadzenLineSeries` for time-series, `RadzenDonutSeries` for breakdowns
- `RadzenDataGrid` for tabular data
- Loads data from `StatisticsApiService`

In [ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor):
- Auto-refresh via `Timer` with `InvokeAsync(StateHasChanged)`
- `IDisposable` to clean up timer
- Progress bars for utilization visualization

In [ClientManager.AdminUI/Services/StatisticsApiService.cs](ClientManager.AdminUI/Services/StatisticsApiService.cs):
- HTTP client wrapper with typed DTOs
- Existing methods: `GetUsageTimeSeriesAsync`, `GetClientUsageBreakdownAsync`, `GetGlobalUsageStatsAsync`

## Steps

### 1. Add `GetHistoricalUsageAsync` to `StatisticsApiService`

Edit [ClientManager.AdminUI/Services/StatisticsApiService.cs](ClientManager.AdminUI/Services/StatisticsApiService.cs):

Add a method to call the historical-usage endpoint (from timed-statistics plan):

```csharp
public async Task<HistoricalUsageData?> GetHistoricalUsageAsync(
    string filterType, string targetId, string? clientId,
    DateTime from, DateTime to, string granularity)
{
    var url = $"api/statistics/historical-usage?filterType={Uri.EscapeDataString(filterType)}"
        + $"&targetId={Uri.EscapeDataString(targetId)}"
        + $"&from={from:O}&to={to:O}"
        + $"&granularity={Uri.EscapeDataString(granularity)}";
    if (clientId is not null)
    {
        url += $"&clientId={Uri.EscapeDataString(clientId)}";
    }
    return await _httpClient.GetFromJsonAsync<HistoricalUsageData>(url);
}
```

Add the DTO:

```csharp
public record HistoricalUsageData(
    string TargetId, string TargetType, string Granularity,
    List<HistoricalUsagePoint> Points);

public record HistoricalUsagePoint(
    DateTime Timestamp, long GrantedCount, long DeniedCount);
```

### 2. Create `Monitor/Monitor.razor`

File: `ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor`

Route: `/monitor`

**Page structure:**

```
┌─────────────────────────────────────────────────────────┐
│ Monitor                                                  │
│ Real-time request analytics across services and clients. │
├────────────────────┬────────────────────────────────────┤
│ Filter: Service ▼  │  Target: [auth-service ▼]          │
│                    │  Client: [All clients ▼]           │
├────────────────────┴────────────────────────────────────┤
│                                                          │
│  ┌─── Request Traffic (line chart) ────────────────────┐│
│  │  ▪ Granted requests (solid line, primary color)     ││
│  │  ▪ Denied requests (solid line, red/danger)         ││
│  │  ▪ Rate limit cap (dashed line, muted)              ││
│  │  X-axis: time (5-min buckets, last hour)            ││
│  └──────────────────────────────────────────────────────┘│
│                                                          │
│  ┌─── Client Breakdown (table) ────────────────────────┐│
│  │ Client │ Service │ Req (last 5m) │ Cap │ Remaining  ││
│  │        │         │ Denied (5m)   │     │ Status     ││
│  └──────────────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────┘
```

**Injected services:**
```razor
@page "/monitor"
@inject StatisticsApiService StatsService
@inject ServiceApiService ServiceService
@inject ResourcePoolApiService PoolService
@inject ClientApiService ClientService
@implements IDisposable
```

**State:**
- `_selectedServiceId` — currently selected service for drilldown
- `_selectedClientId` — optional client filter (null = all clients)
- `_services` / `_clients` — dropdown data
- `_chartPoints` — list of chart data points with granted, denied, cap values
- `_clientRows` — table data: per-client breakdown for the selected service
- `_timer` — auto-refresh every 30 seconds
- `_loading`, `_error`

**Data loading logic (`LoadDataAsync`):**

1. Fetch historical usage for the selected service from the last hour at `FiveMinute` granularity:
   ```csharp
   var from = DateTime.UtcNow.AddHours(-1);
   var to = DateTime.UtcNow;
   var usage = await StatsService.GetHistoricalUsageAsync(
       "Service", _selectedServiceId, _selectedClientId, from, to, "FiveMinute");
   ```

2. Fetch the current rate limit cap from the usage-timeseries endpoint (which returns cap points):
   ```csharp
   var timeSeries = await StatsService.GetUsageTimeSeriesAsync(
       "Service", _selectedServiceId, null);
   ```

3. Fetch per-client breakdown for the selected service:
   ```csharp
   var breakdown = await StatsService.GetClientUsageBreakdownAsync(
       "Service", _selectedServiceId, null);
   ```

4. For the table: for each client in the breakdown, fetch their individual historical usage for the most recent 5-minute bucket to get their granted/denied counts:
   ```csharp
   var recentFrom = DateTime.UtcNow.AddMinutes(-5);
   foreach (var client in breakdown.Entries)
   {
       var clientUsage = await StatsService.GetHistoricalUsageAsync(
           "Service", _selectedServiceId, client.ClientId, recentFrom, to, "FiveMinute");
       // Build table row with granted, denied, cap, remaining, status
   }
   ```

**Chart:**
```razor
<RadzenChart Style="height: 350px;">
    <RadzenLineSeries Data="@_grantedSeries" CategoryProperty="Label"
        ValueProperty="Value" Title="Granted" Stroke="var(--color-primary)" />
    <RadzenLineSeries Data="@_deniedSeries" CategoryProperty="Label"
        ValueProperty="Value" Title="Denied" Stroke="var(--color-danger)" />
    <RadzenLineSeries Data="@_capSeries" CategoryProperty="Label"
        ValueProperty="Value" Title="Cap" LineType="LineType.Dashed"
        Stroke="var(--color-text-secondary)" />
    <RadzenCategoryAxis />
    <RadzenValueAxis />
    <RadzenLegend Position="LegendPosition.Bottom" />
</RadzenChart>
```

**Table:**
```razor
<RadzenDataGrid Data="@_clientRows" TItem="MonitorClientRow"
    AllowSorting="true" AllowPaging="true" PageSize="15">
    <Columns>
        <RadzenDataGridColumn TItem="MonitorClientRow" Property="ClientName" Title="Client" />
        <RadzenDataGridColumn TItem="MonitorClientRow" Property="ServiceName" Title="Service" />
        <RadzenDataGridColumn TItem="MonitorClientRow" Property="GrantedLast5Min" Title="Req (5m)" Width="100px" />
        <RadzenDataGridColumn TItem="MonitorClientRow" Property="DeniedLast5Min" Title="Denied (5m)" Width="110px" />
        <RadzenDataGridColumn TItem="MonitorClientRow" Property="RateLimitCap" Title="Cap" Width="80px" />
        <RadzenDataGridColumn TItem="MonitorClientRow" Property="Remaining" Title="Remaining" Width="110px" />
        <RadzenDataGridColumn TItem="MonitorClientRow" Title="Status" Width="120px">
            <Template Context="row">
                @if (row.DeniedLast5Min > 0)
                {
                    <span class="cm-badge" style="background: #fee2e2; color: #991b1b;">Hitting Limit</span>
                }
                else if (row.Remaining <= row.RateLimitCap * 0.1)
                {
                    <span class="cm-badge" style="background: #fef3c7; color: #92400e;">Near Limit</span>
                }
                else
                {
                    <span class="cm-badge cm-badge--success">Healthy</span>
                }
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>
```

**Auto-refresh:**
```csharp
_timer = new Timer(async _ =>
{
    await LoadDataAsync();
    await InvokeAsync(StateHasChanged);
}, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
```

**Inner records:**
```csharp
private record ChartPoint(string Label, double Value);
private record MonitorClientRow(
    string ClientId, string ClientName, string ServiceName,
    long GrantedLast5Min, long DeniedLast5Min,
    int RateLimitCap, int Remaining);
```

### 3. Add Monitor CSS

File: `ClientManager.AdminUI/wwwroot/css/monitor.css`

Minimal additions — the page mostly reuses `cm-dashboard__chart-card`, `cm-dashboard__table-card`, `cm-dashboard__filters`, and `cm-list-page__table-card` patterns. Add:

```css
.cm-monitor__filters {
    display: flex;
    gap: var(--space-sm);
    align-items: center;
    margin-bottom: var(--space-lg);
    flex-wrap: wrap;
}

.cm-monitor__chart-card {
    background: var(--color-bg-card);
    border-radius: var(--radius-md);
    box-shadow: var(--shadow-sm);
    border: 1px solid var(--color-border);
    padding: var(--space-lg);
    margin-bottom: var(--space-lg);
}
```

Link it in [App.razor](ClientManager.AdminUI/Components/App.razor):
```html
<link rel="stylesheet" href="css/monitor.css">
```

### 4. Add Monitor to the sidebar

Edit [ClientManager.AdminUI/Components/Layout/NavMenu.razor](ClientManager.AdminUI/Components/Layout/NavMenu.razor):

Add Monitor as the last item in **Section 2 (Services)**, after "Rate Limits" and before the divider:

```razor
<!-- Section 2: Services -->
<li class="cm-sidebar__nav-item">
    <NavLink class="nav-link" href="services">
        <RadzenIcon Icon="build" class="cm-sidebar__nav-icon" />
        Services
    </NavLink>
</li>
<li class="cm-sidebar__nav-item">
    <NavLink class="nav-link" href="rate-limits">
        <RadzenIcon Icon="speed" class="cm-sidebar__nav-icon" />
        Rate Limits
    </NavLink>
</li>
<li class="cm-sidebar__nav-item">
    <NavLink class="nav-link" href="monitor">
        <RadzenIcon Icon="monitoring" class="cm-sidebar__nav-icon" />
        Monitor
    </NavLink>
</li>

<li class="cm-sidebar__divider"></li>
```

Final sidebar sections:
- **Section 1**: Dashboard, Clients
- **Section 2**: Services, Rate Limits, **Monitor**
- **Section 3**: Resource Pools, Quotas, Active Allocations

## Verification

- Solution compiles without errors
- Navigating to `/monitor` shows the Monitor page with filter dropdowns, a time-series chart, and a client breakdown table
- Selecting a different service reloads the chart and table with that service's data
- Filtering by client shows only that client's data in the chart
- The table shows accurate Granted/Denied counts for the last 5 minutes per client
- Status badges correctly reflect: "Hitting Limit" (any denials), "Near Limit" (≤10% remaining), "Healthy" (all good)
- The page auto-refreshes every 30 seconds without flicker
- The Monitor nav item appears in the sidebar in Section 2 (Services group) with the `monitoring` icon
- Clicking the "Requests / min" stat card on the dashboard navigates to `/monitor`
