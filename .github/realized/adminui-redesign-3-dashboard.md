# Plan: AdminUI Redesign — Step 3: Dashboard Page

> **Status**: ✅ Completed
> **Prerequisite**: [adminui-redesign-2-layout-nav.md](adminui-redesign-2-layout-nav.md)
> **Next**: [adminui-redesign-4-api-endpoints.md](adminui-redesign-4-api-endpoints.md)
> **Parent**: [adminui-redesign-overview.md](adminui-redesign-overview.md)

## TL;DR

Rebuild the Dashboard page to match the Metric design: a welcome header, 5 stat cards in a top row, a line chart + donut chart side by side with service/pool/client filter dropdowns, and a client summary data table at the bottom. Charts contextually switch between rate-limit data (service filter) and slot-acquisition data (pool filter).

## Reference Pattern

**Design Reference**: [Dribbble Metric Dashboard Video](https://cdn.dribbble.com/userupload/42834013/file/original-333c4f78536a41262709503fae3c7342.mp4)

Key dashboard elements from the reference:
- "Welcome / To your [X] at a glance." header at top-left, search at top-right
- 4 horizontal stat cards (first one filled primary, rest outlined) — we use 5
- Below: line chart (left, ~60% width) + donut chart (right, ~40% width) in a card
- Below: data table with search and filter dropdown in a card

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](../../ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- Currently shows 4 stat cards + a resource pool utilization table
- Uses `StatisticsApiService` for `SystemOverview` and `ResourcePoolStatistics`

## Steps

### 1. Create dashboard CSS file

Create `ClientManager.AdminUI/wwwroot/css/dashboard.css`:

```css
.cm-dashboard__header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    margin-bottom: var(--space-xl);
}

.cm-dashboard__stats {
    display: grid;
    grid-template-columns: repeat(5, 1fr);
    gap: var(--space-md);
    margin-bottom: var(--space-xl);
}

.cm-dashboard__charts {
    display: grid;
    grid-template-columns: 3fr 2fr;
    gap: var(--space-md);
    margin-bottom: var(--space-xl);
}

.cm-dashboard__chart-card {
    background: var(--color-bg-card);
    border-radius: var(--radius-md);
    box-shadow: var(--shadow-sm);
    border: 1px solid var(--color-border);
    padding: var(--space-lg);
}

.cm-dashboard__chart-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: var(--space-md);
}

.cm-dashboard__chart-title {
    font-size: var(--font-size-md);
    font-weight: 600;
    color: var(--color-text-primary);
}

.cm-dashboard__filters {
    display: flex;
    gap: var(--space-sm);
    align-items: center;
}

.cm-dashboard__table-card {
    background: var(--color-bg-card);
    border-radius: var(--radius-md);
    box-shadow: var(--shadow-sm);
    border: 1px solid var(--color-border);
    padding: var(--space-lg);
}

@media (max-width: 1200px) {
    .cm-dashboard__stats {
        grid-template-columns: repeat(3, 1fr);
    }
    .cm-dashboard__charts {
        grid-template-columns: 1fr;
    }
}
```

### 2. Reference dashboard CSS in `App.razor`

Add to `<head>` in `App.razor`:

```html
<link rel="stylesheet" href="css/dashboard.css">
```

### 3. Rebuild `Dashboard.razor` — Page header and stat cards

Replace the current `Dashboard.razor` content. The stat cards section:

```razor
@page "/"
@inject StatisticsApiService StatsService

<div class="cm-page-header">
    <h1>Welcome</h1>
    <p>Your system's performance at a glance.</p>
</div>

@if (_loading)
{
    <RadzenProgressBarCircular ShowValue="false" Mode="ProgressBarMode.Indeterminate" />
}
else if (_error is not null)
{
    <RadzenAlert AlertStyle="AlertStyle.Danger" Shade="Shade.Light">@_error</RadzenAlert>
}
else
{
    <!-- 5 Stat Cards -->
    <div class="cm-dashboard__stats">
        <div class="cm-stat-card cm-stat-card--primary">
            <div class="cm-stat-card__value">@_overview?.EnabledClients / @_overview?.TotalClients</div>
            <div class="cm-stat-card__label">Clients</div>
        </div>
        <div class="cm-stat-card">
            <div class="cm-stat-card__value">@_overview?.EnabledServices / @_overview?.TotalServices</div>
            <div class="cm-stat-card__label">Services</div>
        </div>
        <div class="cm-stat-card">
            <div class="cm-stat-card__value">@_overview?.TotalResourcePools</div>
            <div class="cm-stat-card__label">Resource Pools</div>
        </div>
        <div class="cm-stat-card">
            <div class="cm-stat-card__value">@_globalUsage</div>
            <div class="cm-stat-card__label">Requests / min</div>
        </div>
        <div class="cm-stat-card">
            <div class="cm-stat-card__value">@_acquisitionPct%</div>
            <div class="cm-stat-card__label">Pool Acquisition</div>
        </div>
    </div>

    <!-- Charts + Table sections follow (see steps 4 & 5) -->
}
```

### 4. Add line chart + donut chart with filter controls

Below the stat cards, add a charts section with filter dropdowns. The filters control what data both charts display:

```razor
<!-- Chart filter bar -->
<div class="cm-dashboard__charts">
    <div class="cm-dashboard__chart-card">
        <div class="cm-dashboard__chart-header">
            <span class="cm-dashboard__chart-title">Usage Over Time</span>
            <div class="cm-dashboard__filters">
                <RadzenDropDown @bind-Value="_selectedFilterType"
                    Data="@_filterTypes" TextProperty="Label" ValueProperty="Value"
                    Placeholder="Filter by..." Change="@OnFilterChanged" Style="width:160px;" />
                <RadzenDropDown @bind-Value="_selectedTargetId"
                    Data="@_filterTargets" TextProperty="Name" ValueProperty="Id"
                    Placeholder="Select target..." Change="@OnFilterChanged" Style="width:180px;" />
                <RadzenDropDown @bind-Value="_selectedClientIds" Multiple="true"
                    Data="@_clients" TextProperty="Name" ValueProperty="Id"
                    Placeholder="All clients" Style="width:200px;"
                    Change="@OnFilterChanged" />
            </div>
        </div>
        <RadzenChart Style="height: 300px;">
            <RadzenLineSeries Data="@_usageOverTime" CategoryProperty="Timestamp"
                ValueProperty="Value" Title="Usage" />
            <RadzenLineSeries Data="@_capOverTime" CategoryProperty="Timestamp"
                ValueProperty="Value" Title="Limit" LineType="LineType.Dashed" />
            <RadzenCategoryAxis FormatString="{0:HH:mm}" />
            <RadzenValueAxis />
            <RadzenLegend Position="LegendPosition.Bottom" />
        </RadzenChart>
    </div>

    <div class="cm-dashboard__chart-card">
        <div class="cm-dashboard__chart-header">
            <span class="cm-dashboard__chart-title">Usage Per Client</span>
        </div>
        <RadzenChart Style="height: 300px;">
            <RadzenDonutSeries Data="@_perClientUsage" CategoryProperty="ClientName"
                ValueProperty="Value" Title="Per Client" />
            <RadzenLegend Position="LegendPosition.Bottom" />
        </RadzenChart>
    </div>
</div>
```

Key behavior:
- Filter Type dropdown: "Service" or "Resource Pool"
- Target dropdown: populated with services or pools depending on filter type
- Client multi-select: optionally filter to specific clients
- When filter type = Service: line chart shows request rate over time vs rate limit cap; donut shows per-client request distribution
- When filter type = Resource Pool: line chart shows slot acquisition over time vs max slots; donut shows per-client slot usage

### 5. Add client summary table

Below the charts:

```razor
<div class="cm-dashboard__table-card">
    <div class="cm-dashboard__chart-header">
        <span class="cm-dashboard__chart-title">Client Overview</span>
        <RadzenTextBox Placeholder="Search clients..." @bind-Value="_tableSearch"
            Change="@OnTableSearchChanged" Style="width: 250px;" />
    </div>

    <RadzenDataGrid Data="@_filteredClientSummaries" TItem="ClientSummaryRow"
        AllowSorting="true" AllowPaging="true" PageSize="10"
        PagerHorizontalAlign="HorizontalAlign.Center">
        <Columns>
            <RadzenDataGridColumn TItem="ClientSummaryRow" Property="ClientId" Title="Client ID" Width="150px" />
            <RadzenDataGridColumn TItem="ClientSummaryRow" Property="DisplayName" Title="Who Am I?" Width="150px" />
            <RadzenDataGridColumn TItem="ClientSummaryRow" Property="AccessibleServices" Title="Services" Width="100px" />
            <RadzenDataGridColumn TItem="ClientSummaryRow" Property="TotalRateLimitCap" Title="Rate Limit Cap" Width="140px" />
            <RadzenDataGridColumn TItem="ClientSummaryRow" Property="AccessiblePools" Title="Pools" Width="100px" />
            <RadzenDataGridColumn TItem="ClientSummaryRow" Property="SlotsDisplay" Title="Slots (Used/Total)" Width="160px" />
        </Columns>
    </RadzenDataGrid>
</div>
```

### 6. Add the `@code` block with models and data loading

In the `@code` block, define:

```csharp
// Existing fields
private bool _loading = true;
private string? _error;
private SystemOverview? _overview;

// New fields for 5th stat cards
private string _globalUsage = "—";
private int _acquisitionPct = 0;

// Filter state
private string _selectedFilterType = "Service";
private string? _selectedTargetId;
private IEnumerable<string>? _selectedClientIds;
private string? _tableSearch;

// Filter options (loaded from API)
private List<FilterOption> _filterTypes = new()
{
    new("Service", "Service"),
    new("Resource Pool", "ResourcePool")
};
private List<NamedItem> _filterTargets = new();
private List<NamedItem> _clients = new();

// Chart data (loaded from new API endpoints)
private List<TimeSeriesPoint> _usageOverTime = new();
private List<TimeSeriesPoint> _capOverTime = new();
private List<ClientUsagePoint> _perClientUsage = new();

// Table data
private List<ClientSummaryRow> _clientSummaries = new();
private List<ClientSummaryRow> _filteredClientSummaries = new();

// Local DTOs
record FilterOption(string Label, string Value);
record NamedItem(string Id, string Name);
record TimeSeriesPoint(DateTime Timestamp, double Value);
record ClientUsagePoint(string ClientName, double Value);
record ClientSummaryRow(string ClientId, string DisplayName, int AccessibleServices,
    string TotalRateLimitCap, int AccessiblePools, string SlotsDisplay);
```

**Note**: The actual data loading depends on the new API endpoints defined in Step 4 (api-endpoints). For now, wire up `OnInitializedAsync` to load overview stats and client/service/pool lists. Chart and table data will use placeholder/empty data until the API endpoints are implemented.

### 7. Reference dashboard CSS in App.razor

Ensure `css/dashboard.css` is linked in `App.razor` `<head>`.

## Verification

- Dashboard page compiles without errors

### Required: Browser Verification

Before marking this step complete, the implementer **must**:
1. Ensure the API project is running in a background terminal (start it if not already running).
2. Ensure the AdminUI project is running in a background terminal (restart it to pick up changes).
3. Open the AdminUI Dashboard page in the shared browser (using `open_browser_page`).
4. Take a screenshot and verify:
   - 5 stat cards render in a horizontal row
   - First stat card (Clients) has indigo/purple background
   - Charts area shows two cards side by side (line chart left, donut right)
   - Filter dropdowns render and are interactive (even if data is empty/placeholder)
   - Client summary table renders with correct columns using RadzenDataGrid
   - No console errors
5. Interact with the filter dropdowns and take a screenshot showing they open/respond.
6. Share screenshots with the user for sign-off before proceeding to the next step.
