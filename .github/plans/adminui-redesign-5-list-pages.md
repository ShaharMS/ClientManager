# Plan: AdminUI Redesign — Step 5: List Pages

> **Status**: 🔲 Not started
> **Prerequisite**: [adminui-redesign-4-api-endpoints.md](adminui-redesign-4-api-endpoints.md)
> **Next**: [adminui-redesign-6-editor-pages.md](adminui-redesign-6-editor-pages.md)
> **Parent**: [adminui-redesign-overview.md](adminui-redesign-overview.md)

## TL;DR

Restyle all list pages (Clients, Services, Resource Pools, Global Rate Limits, Active Allocations) to use `RadzenDataGrid` with sorting, filtering, pagination, and the Metric card-based visual style — replacing plain Bootstrap tables.

## Reference Pattern

**Design Reference**: [Dribbble Metric Dashboard Video](https://cdn.dribbble.com/userupload/42834013/file/original-333c4f78536a41262709503fae3c7342.mp4)

Key table elements from the reference:
- Table wrapped in a white card with shadow and rounded corners
- Header row with title (left) and search + filter controls (right)
- Clean table rows with subtle borders
- Pagination at the bottom

In [ClientManager.AdminUI/Components/Pages/Clients/ClientList.razor](../../ClientManager.AdminUI/Components/Pages/Clients/ClientList.razor):
- Currently a Bootstrap `<table>` with manual rows
- Has Create/Edit/Delete buttons — keep the same actions

All list pages follow the same pattern. Apply the same transformation to each.

## Steps

### 1. Create shared list page CSS

Create `ClientManager.AdminUI/wwwroot/css/list-pages.css`:

```css
.cm-list-page__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: var(--space-lg);
}

.cm-list-page__actions {
    display: flex;
    gap: var(--space-sm);
    align-items: center;
}

.cm-list-page__table-card {
    background: var(--color-bg-card);
    border-radius: var(--radius-md);
    box-shadow: var(--shadow-sm);
    border: 1px solid var(--color-border);
    padding: var(--space-lg);
}

/* Status badges */
.cm-badge {
    display: inline-flex;
    align-items: center;
    padding: 0.2rem 0.6rem;
    border-radius: 999px;
    font-size: var(--font-size-xs);
    font-weight: 600;
}

.cm-badge--success {
    background: #dcfce7;
    color: #166534;
}

.cm-badge--muted {
    background: #f1f5f9;
    color: #64748b;
}
```

Reference in `App.razor` `<head>`:

```html
<link rel="stylesheet" href="css/list-pages.css">
```

### 2. Restyle `ClientList.razor`

Replace the current Bootstrap table with:

```razor
<div class="cm-page-header">
    <h1>Clients</h1>
    <p>Manage client configurations and access control.</p>
</div>

<div class="cm-list-page__table-card">
    <div class="cm-list-page__header">
        <RadzenTextBox Placeholder="Search clients..." @bind-Value="_search"
            Change="@OnSearchChanged" Style="width: 280px;" />
        <RadzenButton Text="Create Client" Icon="add" Click="@(() => Nav.NavigateTo("clients/create"))"
            ButtonStyle="ButtonStyle.Primary" />
    </div>

    <RadzenDataGrid Data="@_filteredClients" TItem="ClientConfiguration"
        AllowSorting="true" AllowPaging="true" PageSize="15"
        PagerHorizontalAlign="HorizontalAlign.Center">
        <Columns>
            <RadzenDataGridColumn TItem="ClientConfiguration" Property="Id" Title="ID" Width="150px" />
            <RadzenDataGridColumn TItem="ClientConfiguration" Property="Name" Title="Name" />
            <RadzenDataGridColumn TItem="ClientConfiguration" Title="Status" Width="100px">
                <Template Context="client">
                    @if (client.IsEnabled)
                    {
                        <span class="cm-badge cm-badge--success">Enabled</span>
                    }
                    else
                    {
                        <span class="cm-badge cm-badge--muted">Disabled</span>
                    }
                </Template>
            </RadzenDataGridColumn>
            <RadzenDataGridColumn TItem="ClientConfiguration" Title="Services" Width="100px">
                <Template Context="client">@client.Services.Count</Template>
            </RadzenDataGridColumn>
            <RadzenDataGridColumn TItem="ClientConfiguration" Title="Pools" Width="100px">
                <Template Context="client">@client.ResourcePools.Count</Template>
            </RadzenDataGridColumn>
            <RadzenDataGridColumn TItem="ClientConfiguration" Title="Actions" Width="160px" Sortable="false">
                <Template Context="client">
                    <RadzenButton Icon="edit" Size="ButtonSize.Small" Variant="Variant.Text"
                        Click="@(() => Nav.NavigateTo($"clients/edit/{client.Id}"))" />
                    <RadzenButton Icon="delete" Size="ButtonSize.Small" Variant="Variant.Text"
                        ButtonStyle="ButtonStyle.Danger"
                        Click="@(() => DeleteClient(client.Id))" />
                </Template>
            </RadzenDataGridColumn>
        </Columns>
    </RadzenDataGrid>
</div>
```

### 3. Restyle `ServiceList.razor`

Same pattern as ClientList but with Service-specific columns (ID, Name, Enabled status) and simpler layout. Use `RadzenDataGrid` with `Service` as TItem.

### 4. Restyle `ResourcePoolList.razor`

Same pattern with columns: ID, Name, Max Slots, Allocation TTL. Format TTL as human-readable duration.

### 5. Restyle `GlobalRateLimitList.razor`

Same pattern with columns: ID, Target ID, Target Type (badge), Strategy (badge), Max Requests, Window.

### 6. Restyle `ActiveAllocations.razor`

Replace the current auto-refresh table with a `RadzenDataGrid`. Keep the 10-second auto-refresh timer. Add utilization as a `RadzenProgressBar` inside a template column. Add a visual indicator for pools at capacity (red badge or progress bar color).

```razor
<RadzenDataGridColumn TItem="ResourcePoolStatistics" Title="Utilization" Width="200px">
    <Template Context="pool">
        @{
            var pct = pool.MaxSlots > 0 ? (int)(pool.ActiveAllocations * 100.0 / pool.MaxSlots) : 0;
            var style = pct >= 100 ? ButtonStyle.Danger : pct >= 75 ? ButtonStyle.Warning : ButtonStyle.Success;
        }
        <RadzenProgressBar Value="@pct" Max="100" ShowValue="true"
            ProgressBarStyle="@style" Style="height: 20px;" />
    </Template>
</RadzenDataGridColumn>
```

## Verification

- All list page files compile without errors

### Required: Browser Verification

Before marking this step complete, the implementer **must**:
1. Ensure the API project is running in a background terminal (start it if not already running).
2. Ensure the AdminUI project is running in a background terminal (restart it to pick up changes).
3. Open the AdminUI in the shared browser and navigate to **each** of the 5 list pages. For each page, take a screenshot and verify:
   - `RadzenDataGrid` renders inside a white card container
   - Sorting works on at least one column (click a header)
   - Search/filter text box is present
   - Create button is visible and styled
   - Status columns show colored badges (green for enabled, gray for disabled)
4. On the Active Allocations page, wait 10+ seconds and take a second screenshot to confirm auto-refresh is working.
5. On the Clients list page, click Edit on a client to verify navigation to the editor works.
6. Share all screenshots with the user for sign-off before proceeding to the next step.
