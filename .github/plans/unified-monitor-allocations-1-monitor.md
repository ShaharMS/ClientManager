# Plan: Unified Monitor & Allocations — Step 1: Monitor Page Redesign

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [unified-monitor-allocations-2-allocations.md](unified-monitor-allocations-2-allocations.md)
> **Parent**: [unified-monitor-allocations-overview.md](unified-monitor-allocations-overview.md)

## TL;DR

Redesign `Monitor.razor` to the unified layout: add an "All services" option to the service dropdown, replace the line chart with a per-client stacked area chart + cap line (one chart card per selected target), enhance the client breakdown table with utilization bars, insert an `<hr>` separator, and add a new all-services summary table with 0–100% progress bars at the bottom.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- "All" target option pattern: `AllTargetsId = "__all__"` with concat to target list
- Multi-select client dropdown: `<RadzenDropDown @bind-Value="_selectedClientIds" Multiple="true" ...>`
- Chart series rendering via `@foreach (var series in _chartSeries)` with `RadzenLineSeries`
- Filter change handler: `OnFilterChanged` method that reloads chart + breakdown data

In [ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor](ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor):
- Per-pool table with `RadzenProgressBar` utilization column pattern
- Status badge pattern (At Capacity / Contention / Available)
- Timer-based auto-refresh every 10 seconds

In [ClientManager.AdminUI/wwwroot/css/monitor.css](ClientManager.AdminUI/wwwroot/css/monitor.css):
- `cm-monitor__filters` and `cm-monitor__chart-card` classes already defined

## Steps

### 1. Add "All services" option and update dropdowns

In `Monitor.razor` `@code` block, add a sentinel constant and update service list initialization:

```csharp
private const string AllServicesId = "__all__";
```

In `OnInitializedAsync`, prepend an "All services" option to `_services`:

```csharp
_serviceOptions = new List<NamedItem> { new(AllServicesId, "All Services") }
    .Concat(services.Select(s => new NamedItem(s.Id, s.Name))).ToList();
_selectedServiceId = AllServicesId;
```

Change the service dropdown to use `_serviceOptions` (type `List<NamedItem>`) instead of `_services`. Keep the client dropdown as multi-select (`IEnumerable<string>?`).

### 2. Replace line chart with stacked area charts

Replace the single "Request Traffic" chart card with a loop that renders one chart card per visible service. Each chart card contains:

```razor
@foreach (var targetChart in _targetCharts)
{
    <div class="cm-monitor__chart-card">
        <div class="cm-dashboard__chart-header">
            <span class="cm-dashboard__chart-title">@targetChart.TargetName — Usage</span>
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
                Title="Cap" LineType="LineType.Dashed"
                Stroke="var(--color-text-secondary)" />
            <RadzenCategoryAxis />
            <RadzenValueAxis />
            <RadzenLegend Position="LegendPosition.Bottom" />
        </RadzenChart>
    </div>
}
```

### 3. Build per-client time-series data in LoadDataAsync

For each visible service, fetch per-client historical data to build stacked area series:

```csharp
private List<TargetChartData> _targetCharts = [];

// Inside LoadDataAsync, for each visible service:
var visibleServices = _selectedServiceId == AllServicesId
    ? _allServices
    : _allServices.Where(s => s.Id == _selectedServiceId).ToList();

foreach (var service in visibleServices)
{
    var breakdown = await StatsService.GetClientUsageBreakdownAsync(
        "Service", service.Id, _selectedClientIds);

    var clientAreas = new List<ClientAreaSeries>();
    foreach (var entry in breakdown?.Entries ?? [])
    {
        var history = await StatsService.GetHistoricalUsageAsync(
            "Service", service.Id, entry.ClientId, from, now, "FiveMinute");
        clientAreas.Add(new ClientAreaSeries(entry.ClientName,
            history?.Points.Select(p =>
                new ChartPoint(p.Timestamp.ToLocalTime().ToString("HH:mm"),
                    (double)p.GrantedCount)).ToList() ?? []));
    }

    // Cap line from global rate limit
    var cap = rateLimits
        .FirstOrDefault(r => r.TargetId == service.Id)?.MaxRequests ?? 0;
    var capPoints = /* same timestamps as above, value = cap */;

    _targetCharts.Add(new TargetChartData(service.Name, clientAreas, capPoints));
}
```

### 4. Add record types for chart data

```csharp
private record NamedItem(string Id, string Name);
private record TargetChartData(string TargetName, List<ClientAreaSeries> ClientSeries, List<ChartPoint> CapSeries);
private record ClientAreaSeries(string ClientName, List<ChartPoint> Points);
private record ChartPoint(string Label, double Value);
```

### 5. Enhance the per-client-per-target breakdown table

Below the chart cards, render the client breakdown table. Add a `RadzenProgressBar` utilization column:

```razor
<div class="cm-list-page__table-card" style="margin-top: var(--space-lg);">
    <div class="cm-dashboard__chart-header">
        <span class="cm-dashboard__chart-title">Client Breakdown</span>
    </div>
    <RadzenDataGrid Data="@_clientRows" TItem="MonitorClientRow" ...>
        <Columns>
            <RadzenDataGridColumn Property="ClientName" Title="Client" />
            <RadzenDataGridColumn Property="ServiceName" Title="Service" Width="150px" />
            <RadzenDataGridColumn Property="GrantedLast5Min" Title="Req (5m)" Width="100px" />
            <RadzenDataGridColumn Property="DeniedLast5Min" Title="Denied (5m)" Width="110px" />
            <RadzenDataGridColumn Property="RateLimitCap" Title="Cap" Width="80px" />
            <RadzenDataGridColumn Title="Utilization" Width="180px">
                <Template Context="row">
                    @{
                        var pct = row.RateLimitCap > 0
                            ? (int)(row.GrantedLast5Min * 100.0 / row.RateLimitCap) : 0;
                        var barStyle = pct >= 100 ? ProgressBarStyle.Danger
                            : pct >= 75 ? ProgressBarStyle.Warning : ProgressBarStyle.Success;
                    }
                    <RadzenProgressBar Value="@pct" Max="100" ShowValue="true"
                        ProgressBarStyle="@barStyle" Style="height: 18px;" />
                </Template>
            </RadzenDataGridColumn>
            <RadzenDataGridColumn Title="Status" Width="120px" Sortable="false">
                <!-- existing status badge template -->
            </RadzenDataGridColumn>
        </Columns>
    </RadzenDataGrid>
</div>
```

### 6. Add `<hr>` separator and all-services summary table

After the client breakdown table, insert a separator and a new table of all services (unfiltered):

```razor
<hr class="cm-monitor__separator" />

<div class="cm-list-page__table-card" style="margin-top: var(--space-lg);">
    <div class="cm-dashboard__chart-header">
        <span class="cm-dashboard__chart-title">All Services</span>
    </div>
    <RadzenDataGrid Data="@_allServiceStats" TItem="ServiceSummaryRow" ...>
        <Columns>
            <RadzenDataGridColumn Property="Name" Title="Service" />
            <RadzenDataGridColumn Property="CurrentUsage" Title="Current" Width="100px" />
            <RadzenDataGridColumn Property="Cap" Title="Cap" Width="100px" />
            <RadzenDataGridColumn Title="Utilization" Width="200px">
                <Template Context="row">
                    @{
                        var pct = row.Cap > 0
                            ? (int)(row.CurrentUsage * 100.0 / row.Cap) : 0;
                        var barStyle = pct >= 100 ? ProgressBarStyle.Danger
                            : pct >= 75 ? ProgressBarStyle.Warning : ProgressBarStyle.Success;
                    }
                    <RadzenProgressBar Value="@pct" Max="100" ShowValue="true"
                        ProgressBarStyle="@barStyle" Style="height: 20px;" />
                </Template>
            </RadzenDataGridColumn>
            <RadzenDataGridColumn Title="Status" Width="110px" Sortable="false">
                <!-- At Capacity / Available badge -->
            </RadzenDataGridColumn>
        </Columns>
    </RadzenDataGrid>
</div>
```

### 7. Load all-services summary data (unfiltered)

In `LoadDataAsync`, always fetch all-services summary regardless of dropdown state:

```csharp
private List<ServiceSummaryRow> _allServiceStats = [];

// Fetch all service usage data (independent of dropdown filters)
var allServiceBreakdowns = new List<ServiceSummaryRow>();
foreach (var service in _allServices)
{
    var breakdown = await StatsService.GetClientUsageBreakdownAsync("Service", service.Id, null);
    var totalUsage = breakdown?.Entries.Sum(e => e.Value) ?? 0;
    var cap = rateLimits.FirstOrDefault(r => r.TargetId == service.Id)?.MaxRequests ?? 0;
    allServiceBreakdowns.Add(new ServiceSummaryRow(service.Id, service.Name, (long)totalUsage, cap));
}
_allServiceStats = allServiceBreakdowns;
```

```csharp
private record ServiceSummaryRow(string Id, string Name, long CurrentUsage, int Cap);
```

### 8. Update auto-refresh to 10 seconds

Change the timer from 30 seconds to 10 seconds:

```csharp
_timer = new Timer(async _ => { ... },
    null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
```

### 9. Add CSS for the separator

In `monitor.css`, add:

```css
.cm-monitor__separator {
    border: none;
    border-top: 1px solid var(--color-border);
    margin: var(--space-xl) 0;
}
```

## Verification

- Project compiles without errors (`dotnet build ClientManager.AdminUI`)
- **UI: Navigate to `/monitor` — verify the service dropdown shows "All Services" as default option with individual services below**
- **UI: Verify stacked area chart(s) render with per-client colored filled areas and a dashed cap line**
- **UI: Select a specific service — verify only one chart card appears**
- **UI: Select specific client(s) — verify charts and breakdown table filter to those clients**
- **UI: Verify the Client Breakdown table has a new utilization progress bar column**
- **UI: Verify an `<hr>` separator is visible below the client breakdown table**
- **UI: Verify the All Services summary table appears at the bottom with utilization bars and is always unfiltered**
- **UI: Take a screenshot to confirm no layout breakage or error banners**
