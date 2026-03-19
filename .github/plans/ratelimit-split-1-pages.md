# Plan: Split Global Rate Limits — Step 1: Rate Limits & Quotas Pages

> **Status**: 🔲 Not started
> **Prerequisite**: [timed-statistics-6-api-endpoints.md](timed-statistics-6-api-endpoints.md)
> **Next**: [dashboard-drilldowns-1-card-arrows.md](dashboard-drilldowns-1-card-arrows.md)
> **Parent**: [ratelimit-split-overview.md](ratelimit-split-overview.md)

## TL;DR

Create two new page pairs (list + editor) that replace the unified Global Rate Limits page. "Rate Limits" is scoped to services, "Quotas" is scoped to resource pools. Update the sidebar navigation to place each beneath its parent domain. Remove the old `GlobalRateLimits/` pages.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/GlobalRateLimits/GlobalRateLimitList.razor](ClientManager.AdminUI/Components/Pages/GlobalRateLimits/GlobalRateLimitList.razor):
- Standard list page: `cm-page-header`, `cm-list-page__table-card`, search bar, create button, `RadzenDataGrid`
- Injects `GlobalRateLimitApiService` and `NavigationManager`
- Uses `_loading`, `_error`, `_search` state pattern

In [ClientManager.AdminUI/Components/Pages/GlobalRateLimits/GlobalRateLimitEditor.razor](ClientManager.AdminUI/Components/Pages/GlobalRateLimits/GlobalRateLimitEditor.razor):
- Dual route for new/edit, `EditForm` with `DataAnnotationsValidator`
- Back-link, `cm-editor` card layout, save/cancel actions
- Maps between `RateLimitFormModel` and `GlobalRateLimit` entity

In [ClientManager.AdminUI/Services/GlobalRateLimitApiService.cs](ClientManager.AdminUI/Services/GlobalRateLimitApiService.cs):
- Already has `GetByTargetTypeAsync(GlobalRateLimitTarget targetType)` — no changes needed

## Steps

### 1. Create `RateLimits/RateLimitList.razor`

File: `ClientManager.AdminUI/Components/Pages/RateLimits/RateLimitList.razor`

Route: `/rate-limits`

Clone from `GlobalRateLimitList.razor` with these changes:
- Page header: `"Rate Limits"` / `"Manage rate limits applied to service endpoints."`
- Load data: `await RateLimitApi.GetByTargetTypeAsync(GlobalRateLimitTarget.Service)` instead of `GetAllAsync()`
- Create button: `"Create Rate Limit"`, navigates to `/rate-limits/new`
- Remove the "Target Type" column — all entries are Service type
- Edit action navigates to `/rate-limits/{limit.Id}`
- Search filters by `Id` or `TargetId`

```razor
@page "/rate-limits"
@inject GlobalRateLimitApiService RateLimitApi
@inject NavigationManager Nav

<div class="cm-page-header">
    <h1>Rate Limits</h1>
    <p>Manage rate limits applied to service endpoints.</p>
</div>
```

Grid columns:
- **ID** (150px)
- **Service** (instead of "Target ID")
- **Strategy** (badge)
- **Max Requests** (130px)
- **Window** (100px, formatted)
- **Actions** (edit → `/rate-limits/{id}`, delete)

### 2. Create `RateLimits/RateLimitEditor.razor`

File: `ClientManager.AdminUI/Components/Pages/RateLimits/RateLimitEditor.razor`

Routes: `/rate-limits/new` and `/rate-limits/{Id}`

Clone from `GlobalRateLimitEditor.razor` with these changes:
- Page header: `"Create Rate Limit"` / `"Edit Rate Limit: {Id}"`
- Description: `"Define a rate limit for a service endpoint."` / `"Update rate limit configuration."`
- Back-link: `"← Back to Rate Limits"`, path = `rate-limits`
- **Remove the Target Type dropdown** — hardcode `TargetType = GlobalRateLimitTarget.Service` in the form model and on save
- Rename "Target ID" label to **"Service"** — add a helper note or use a dropdown of available services (stretch: inject `ServiceApiService` and provide a `RadzenDropDown` populated with services)
- Cancel navigates to `/rate-limits`
- Save navigates to `/rate-limits`

```razor
@page "/rate-limits/new"
@page "/rate-limits/{Id}"
@inject GlobalRateLimitApiService RateLimitApi
@inject NavigationManager Nav

<div style="margin-bottom: var(--space-md);">
    <RadzenLink Path="rate-limits" Text="← Back to Rate Limits" Style="font-size: var(--font-size-sm);" />
</div>
```

In the form model initializer:
```csharp
private RateLimitFormModel _model = new() { TargetType = GlobalRateLimitTarget.Service };
```

In `SaveAsync`, always set `TargetType = GlobalRateLimitTarget.Service`.

### 3. Create `Quotas/QuotaList.razor`

File: `ClientManager.AdminUI/Components/Pages/Quotas/QuotaList.razor`

Route: `/quotas`

Clone from `GlobalRateLimitList.razor` with these changes:
- Page header: `"Quotas"` / `"Manage request quotas applied to resource pools."`
- Load data: `await RateLimitApi.GetByTargetTypeAsync(GlobalRateLimitTarget.ResourcePool)`
- Create button: `"Create Quota"`, navigates to `/quotas/new`
- Remove the "Target Type" column — all entries are ResourcePool type
- Edit action navigates to `/quotas/{limit.Id}`
- Rename "Target ID" column to **"Resource Pool"**
- Rename "Max Requests" column to **"Max Requests / Window"** (or keep as-is — these are rate limits on resource pool operations)

```razor
@page "/quotas"
@inject GlobalRateLimitApiService RateLimitApi
@inject NavigationManager Nav

<div class="cm-page-header">
    <h1>Quotas</h1>
    <p>Manage request quotas applied to resource pools.</p>
</div>
```

Grid columns:
- **ID** (150px)
- **Resource Pool** (instead of "Target ID")
- **Strategy** (badge)
- **Max Requests** (130px)
- **Window** (100px, formatted)
- **Actions** (edit → `/quotas/{id}`, delete)

### 4. Create `Quotas/QuotaEditor.razor`

File: `ClientManager.AdminUI/Components/Pages/Quotas/QuotaEditor.razor`

Routes: `/quotas/new` and `/quotas/{Id}`

Clone from `GlobalRateLimitEditor.razor` with these changes:
- Page header: `"Create Quota"` / `"Edit Quota: {Id}"`
- Description: `"Define a request quota for a resource pool."` / `"Update quota configuration."`
- Back-link: `"← Back to Quotas"`, path = `quotas`
- **Remove the Target Type dropdown** — hardcode `TargetType = GlobalRateLimitTarget.ResourcePool`
- Rename "Target ID" label to **"Resource Pool"** — optionally inject `ResourcePoolApiService` for a dropdown
- Cancel navigates to `/quotas`
- Save navigates to `/quotas`

```razor
@page "/quotas/new"
@page "/quotas/{Id}"
@inject GlobalRateLimitApiService RateLimitApi
@inject NavigationManager Nav

<div style="margin-bottom: var(--space-md);">
    <RadzenLink Path="quotas" Text="← Back to Quotas" Style="font-size: var(--font-size-sm);" />
</div>
```

In the form model initializer:
```csharp
private RateLimitFormModel _model = new() { TargetType = GlobalRateLimitTarget.ResourcePool };
```

### 5. Update sidebar navigation

Edit [ClientManager.AdminUI/Components/Layout/NavMenu.razor](ClientManager.AdminUI/Components/Layout/NavMenu.razor):

**Remove** the "Global Rate Limits" nav item.

**Restructure** the sidebar into three logical sections separated by soft dividers. Add "Rate Limits" below "Services" and "Quotas" below "Resource Pools".

The sections are:
- **Section 1**: Dashboard + Clients (general overview)
- **Section 2**: Services + Rate Limits (+ Monitor, added in a later plan)
- **Section 3**: Resource Pools + Quotas + Active Allocations

Use a `<li class="cm-sidebar__divider">` element between sections:

```razor
<!-- Section 1: Overview -->
<li class="cm-sidebar__nav-item">
    <NavLink class="nav-link" href="" Match="NavLinkMatch.All">
        <RadzenIcon Icon="dashboard" class="cm-sidebar__nav-icon" />
        Dashboard
    </NavLink>
</li>
<li class="cm-sidebar__nav-item">
    <NavLink class="nav-link" href="clients">
        <RadzenIcon Icon="people" class="cm-sidebar__nav-icon" />
        Clients
    </NavLink>
</li>

<li class="cm-sidebar__divider"></li>

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

<li class="cm-sidebar__divider"></li>

<!-- Section 3: Resources -->
<li class="cm-sidebar__nav-item">
    <NavLink class="nav-link" href="resource-pools">
        <RadzenIcon Icon="hub" class="cm-sidebar__nav-icon" />
        Resource Pools
    </NavLink>
</li>
<li class="cm-sidebar__nav-item">
    <NavLink class="nav-link" href="quotas">
        <RadzenIcon Icon="tune" class="cm-sidebar__nav-icon" />
        Quotas
    </NavLink>
</li>
<li class="cm-sidebar__nav-item">
    <NavLink class="nav-link" href="allocations">
        <RadzenIcon Icon="swap_horiz" class="cm-sidebar__nav-icon" />
        Active Allocations
    </NavLink>
</li>
```

Icons: `speed` for Rate Limits (reuse from old Global Rate Limits), `tune` for Quotas (distinct icon).

### 6. Add sidebar divider CSS

Edit [ClientManager.AdminUI/wwwroot/css/sidebar.css](ClientManager.AdminUI/wwwroot/css/sidebar.css):

```css
.cm-sidebar__divider {
    height: 1px;
    background: var(--color-border);
    opacity: 0.5;
    margin: var(--space-sm) var(--space-md);
    list-style: none;
}
```

The `opacity: 0.5` keeps the divider softer than a full border — visible enough to suggest grouping without being heavy.

### 6. Delete old Global Rate Limits pages

Delete the entire `ClientManager.AdminUI/Components/Pages/GlobalRateLimits/` folder:
- `GlobalRateLimitList.razor`
- `GlobalRateLimitEditor.razor`

## Verification

- Solution compiles without errors
- Navigating to `/rate-limits` shows only Service-type rate limits
- Navigating to `/quotas` shows only ResourcePool-type rate limits
- Creating a rate limit from `/rate-limits/new` automatically sets `TargetType = Service`
- Creating a quota from `/quotas/new` automatically sets `TargetType = ResourcePool`
- The sidebar shows three sections separated by soft dividers: (Dashboard, Clients) | (Services, Rate Limits) | (Resource Pools, Quotas, Active Allocations)
- The dividers are subtle (50% opacity borders) and visually group related nav items
- The old `/global-rate-limits` route no longer exists (404 or blank)
- Editing existing rate limits/quotas works correctly (loads the entity, saves with correct target type)
- No `TargetType` dropdown appears on either editor page
