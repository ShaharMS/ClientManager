# Plan: ClientManager — Step 13: Admin UI Pages

> **Status**: ✅ Completed
> **Prerequisite**: [client-manager-12-admin-ui-foundation.md](client-manager-12-admin-ui-foundation.md)
> **Next**: None — this is the final step.
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Build all Blazor pages for the admin dashboard: a system overview dashboard, CRUD management pages for clients, services, resource pools, and global rate limits, plus an active allocations monitor. Each page uses the typed HTTP client services from step 12 to call the API.

## Reference Pattern

All pages follow the same Blazor Server component pattern:

```razor
@page "/route"
@inject SomeApiService ApiService

<h1>Page Title</h1>

@if (_loading)
{
    <p>Loading...</p>
}
else
{
    <!-- content -->
}

@code {
    private bool _loading = true;
    private List<T> _items = [];

    protected override async Task OnInitializedAsync()
    {
        _items = await ApiService.GetAllAsync();
        _loading = false;
    }
}
```

CRUD pages follow a list-then-edit pattern:
- List page shows a table with edit/delete actions
- Clicking "Create" or "Edit" navigates to an editor page (or shows a modal)
- Editor page has a form bound to the entity, with Save/Cancel buttons
- Delete shows a confirmation dialog before calling the API

## Steps

### 1. Dashboard page

**File: `ClientManager.AdminUI/Components/Pages/Dashboard.razor`**

Route: `/`

Displays the system overview from the statistics API:
- Total clients (enabled / total)
- Total services (enabled / total)
- Total resource pools
- Active allocations count
- Resource pool utilization bars (per pool: active slots / max slots)

```razor
@page "/"
@inject StatisticsApiService StatsService

<h1>Dashboard</h1>

<div class="row g-3 mb-4">
    <div class="col-md-3">
        <div class="card text-center">
            <div class="card-body">
                <h5 class="card-title">Clients</h5>
                <p class="display-6">@_overview?.EnabledClients / @_overview?.TotalClients</p>
            </div>
        </div>
    </div>
    <!-- Similar cards for Services, Resource Pools, Active Allocations -->
</div>

<!-- Resource pool utilization -->
<h3>Resource Pool Utilization</h3>
<table class="table">
    <thead>
        <tr><th>Pool</th><th>Usage</th><th>Active / Max</th></tr>
    </thead>
    <tbody>
        @foreach (var pool in _poolStats)
        {
            <tr>
                <td>@pool.Name</td>
                <td>
                    <div class="progress">
                        <div class="progress-bar" style="width: @(pool.MaxSlots > 0 ? pool.ActiveAllocations * 100 / pool.MaxSlots : 0)%"></div>
                    </div>
                </td>
                <td>@pool.ActiveAllocations / @pool.MaxSlots</td>
            </tr>
        }
    </tbody>
</table>

@code {
    private SystemOverview? _overview;
    private List<ResourcePoolStatistics> _poolStats = [];

    protected override async Task OnInitializedAsync()
    {
        _overview = await StatsService.GetOverviewAsync();
        _poolStats = await StatsService.GetResourcePoolStatsAsync();
    }
}
```

### 2. Client list page

**File: `ClientManager.AdminUI/Components/Pages/Clients/ClientList.razor`**

Route: `/clients`

Displays a table of all client configurations with columns: ID, Name, Enabled, Service Count, Pool Count, Actions (Edit / Delete).

```razor
@page "/clients"
@inject ClientApiService ClientApi
@inject NavigationManager Nav

<h1>Clients</h1>
<button class="btn btn-primary mb-3" @onclick="() => Nav.NavigateTo('/clients/new')">Create Client</button>

<table class="table table-striped">
    <thead>
        <tr>
            <th>ID</th><th>Name</th><th>Enabled</th>
            <th>Services</th><th>Pools</th><th>Actions</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var client in _clients)
        {
            <tr>
                <td>@client.Id</td>
                <td>@client.Name</td>
                <td>@(client.IsEnabled ? "Yes" : "No")</td>
                <td>@client.Services.Count</td>
                <td>@client.ResourcePools.Count</td>
                <td>
                    <button class="btn btn-sm btn-outline-primary" @onclick="() => Nav.NavigateTo($"/clients/{client.Id}")">Edit</button>
                    <button class="btn btn-sm btn-outline-danger" @onclick="() => DeleteAsync(client.Id)">Delete</button>
                </td>
            </tr>
        }
    </tbody>
</table>

@code {
    private List<ClientConfiguration> _clients = [];

    protected override async Task OnInitializedAsync()
    {
        _clients = await ClientApi.GetAllAsync();
    }

    private async Task DeleteAsync(string id)
    {
        await ClientApi.DeleteAsync(id);
        _clients = await ClientApi.GetAllAsync();
    }
}
```

### 3. Client editor page

**File: `ClientManager.AdminUI/Components/Pages/Clients/ClientEditor.razor`**

Route: `/clients/new` and `/clients/{Id}`

A form for creating or editing a `ClientConfiguration`. Includes:
- Basic fields: ID (readonly on edit), Name, IsEnabled, ContributesToGlobalLimits, ExemptFromGlobalLimits
- Global rate limit section (optional): Strategy, MaxRequests, Window, TokensPerRefill
- Services section: a list of service access entries with inline editing (IsAllowed, rate limit settings, global limit flags)
- Resource pools section: a list of pool entries with MaxSlots

```razor
@page "/clients/new"
@page "/clients/{Id}"
@inject ClientApiService ClientApi
@inject NavigationManager Nav

<h1>@(_isNew ? "Create Client" : $"Edit Client: {Id}")</h1>

<EditForm Model="_model" OnValidSubmit="SaveAsync">
    <DataAnnotationsValidator />

    <div class="mb-3">
        <label class="form-label">ID</label>
        <InputText class="form-control" @bind-Value="_model.Id" disabled="@(!_isNew)" />
    </div>
    <div class="mb-3">
        <label class="form-label">Name</label>
        <InputText class="form-control" @bind-Value="_model.Name" />
    </div>
    <div class="mb-3 form-check">
        <InputCheckbox class="form-check-input" @bind-Value="_model.IsEnabled" />
        <label class="form-check-label">Enabled</label>
    </div>
    <!-- ContributesToGlobalLimits, ExemptFromGlobalLimits checkboxes -->
    <!-- Global rate limit section (collapsible) -->
    <!-- Services section (add/remove/edit entries) -->
    <!-- Resource pools section (add/remove/edit entries) -->

    <button type="submit" class="btn btn-primary">Save</button>
    <button type="button" class="btn btn-secondary" @onclick="() => Nav.NavigateTo('/clients')">Cancel</button>
</EditForm>

@code {
    [Parameter] public string? Id { get; set; }
    private bool _isNew => string.IsNullOrEmpty(Id) || Id == "new";
    private ClientConfiguration _model = new();

    protected override async Task OnInitializedAsync()
    {
        if (!_isNew)
        {
            _model = await ClientApi.GetByIdAsync(Id!) ?? new();
        }
    }

    private async Task SaveAsync()
    {
        if (_isNew)
            await ClientApi.CreateAsync(_model);
        else
            await ClientApi.UpdateAsync(Id!, _model);

        Nav.NavigateTo("/clients");
    }
}
```

> **Key detail**: The `Services` and `ResourcePools` dictionaries are edited inline. Adding a new service entry requires entering a service ID and toggling `IsAllowed`. The rate limit sub-fields appear conditionally when a rate limit is configured for that service entry.

### 4. Service management pages

**File: `ClientManager.AdminUI/Components/Pages/Services/ServiceList.razor`**

Route: `/services`

Same list-table pattern as clients. Columns: ID, Name, Enabled, Actions.

**File: `ClientManager.AdminUI/Components/Pages/Services/ServiceEditor.razor`**

Route: `/services/new` and `/services/{Id}`

Simple form: ID, Name, IsEnabled. Same create/edit pattern as the client editor.

### 5. Resource pool management pages

**File: `ClientManager.AdminUI/Components/Pages/ResourcePools/ResourcePoolList.razor`**

Route: `/resource-pools`

Columns: ID, Name, Max Slots, Allocation TTL, Actions.

**File: `ClientManager.AdminUI/Components/Pages/ResourcePools/ResourcePoolEditor.razor`**

Route: `/resource-pools/new` and `/resource-pools/{Id}`

Form: ID, Name, MaxSlots (number input), AllocationTtlSeconds (number input). The TTL is displayed/edited as seconds and sent to the API as seconds.

### 6. Global rate limit management pages

**File: `ClientManager.AdminUI/Components/Pages/GlobalRateLimits/GlobalRateLimitList.razor`**

Route: `/global-rate-limits`

Columns: ID, Target ID, Target Type (Service/ResourcePool), Strategy, Max Requests, Window, Actions.

**File: `ClientManager.AdminUI/Components/Pages/GlobalRateLimits/GlobalRateLimitEditor.razor`**

Route: `/global-rate-limits/new` and `/global-rate-limits/{Id}`

Form: ID, TargetId, TargetType (dropdown: Service / ResourcePool), Strategy (dropdown: FixedWindow / SlidingWindow / TokenBucket), MaxRequests, WindowSeconds, TokensPerRefill (shown only when Strategy is TokenBucket).

### 7. Active allocations page

**File: `ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor`**

Route: `/allocations`

Displays current resource pool utilization from the statistics API. Shows per-pool stats in a table with progress bars. Auto-refreshes every 10 seconds using a timer.

```razor
@page "/allocations"
@inject StatisticsApiService StatsService
@implements IDisposable

<h1>Active Allocations</h1>
<p class="text-muted">Auto-refreshes every 10 seconds</p>

<table class="table">
    <thead>
        <tr><th>Pool</th><th>Active</th><th>Max</th><th>Available</th><th>Utilization</th></tr>
    </thead>
    <tbody>
        @foreach (var pool in _pools)
        {
            <tr>
                <td>@pool.Name</td>
                <td>@pool.ActiveAllocations</td>
                <td>@pool.MaxSlots</td>
                <td>@pool.AvailableSlots</td>
                <td>
                    <div class="progress">
                        <div class="progress-bar @(pool.AvailableSlots == 0 ? "bg-danger" : "")"
                             style="width: @(pool.MaxSlots > 0 ? pool.ActiveAllocations * 100 / pool.MaxSlots : 0)%">
                        </div>
                    </div>
                </td>
            </tr>
        }
    </tbody>
</table>

@code {
    private List<ResourcePoolStatistics> _pools = [];
    private Timer? _timer;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
        _timer = new Timer(async _ =>
        {
            await RefreshAsync();
            await InvokeAsync(StateHasChanged);
        }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    private async Task RefreshAsync()
    {
        _pools = await StatsService.GetResourcePoolStatsAsync();
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
```

## Verification

- `dotnet build` succeeds for the entire solution
- Dashboard page loads and displays statistics from the API
- Client list page shows all clients with correct counts
- Client editor can create a new client and save it via the API
- Client editor can load, modify, and save an existing client
- Client editor supports adding/removing service access entries and resource pool entries inline
- Service, Resource Pool, and Global Rate Limit list/editor pages follow the same CRUD pattern
- Delete operations show correct behavior (item removed from list)
- Active allocations page displays pool utilization and auto-refreshes
- Navigation sidebar links route to the correct pages
- All pages handle loading states (show "Loading..." while awaiting API)
- All pages handle API errors gracefully (show error message if API is unreachable)
