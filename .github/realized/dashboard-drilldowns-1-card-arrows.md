# Plan: Dashboard Drilldowns — Step 1: Stat Card Arrows

> **Status**: ✅ Completed
> **Prerequisite**: [ratelimit-split-1-pages.md](../realized/ratelimit-split-1-pages.md)
> **Next**: [dashboard-drilldowns-2-monitor-page.md](dashboard-drilldowns-2-monitor-page.md)
> **Parent**: [dashboard-drilldowns-overview.md](dashboard-drilldowns-overview.md)

## TL;DR

Add clickable arrow icons to the top-right corner of each of the 5 dashboard stat cards. Three of them navigate to list pages (Clients, Services, Resource Pools), one navigates to a new Monitor page, and one navigates to Active Allocations. This also adds the CSS for the arrow affordance and updates the stat card hover behavior.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- Five stat cards rendered as `<div class="cm-stat-card">` blocks
- Each contains `cm-stat-card__value` and `cm-stat-card__label` children
- First card has `cm-stat-card--primary` modifier

In [ClientManager.AdminUI/wwwroot/css/layout.css](ClientManager.AdminUI/wwwroot/css/layout.css):
- `.cm-stat-card` base styles with hover elevation
- `.cm-stat-card--primary` colored variant

## Steps

### 1. Add arrow markup to each stat card in Dashboard.razor

Edit [ClientManager.AdminUI/Components/Pages/Dashboard.razor](ClientManager.AdminUI/Components/Pages/Dashboard.razor).

Replace the 5 stat card `<div>` blocks with this structure (each card gets a wrapper that is clickable and an arrow icon):

```razor
<div class="cm-stat-card cm-stat-card--primary cm-stat-card--clickable" @onclick='() => Nav.NavigateTo("/clients")'>
    <div class="cm-stat-card__arrow">
        <RadzenIcon Icon="north_east" />
    </div>
    <div class="cm-stat-card__value">@_overview?.TotalClients</div>
    <div class="cm-stat-card__label">Clients</div>
</div>
<div class="cm-stat-card cm-stat-card--clickable" @onclick='() => Nav.NavigateTo("/services")'>
    <div class="cm-stat-card__arrow">
        <RadzenIcon Icon="north_east" />
    </div>
    <div class="cm-stat-card__value">@_overview?.TotalServices</div>
    <div class="cm-stat-card__label">Services</div>
</div>
<div class="cm-stat-card cm-stat-card--clickable" @onclick='() => Nav.NavigateTo("/resource-pools")'>
    <div class="cm-stat-card__arrow">
        <RadzenIcon Icon="north_east" />
    </div>
    <div class="cm-stat-card__value">@_overview?.TotalResourcePools</div>
    <div class="cm-stat-card__label">Resource Pools</div>
</div>
<div class="cm-stat-card cm-stat-card--clickable" @onclick='() => Nav.NavigateTo("/monitor")'>
    <div class="cm-stat-card__arrow">
        <RadzenIcon Icon="north_east" />
    </div>
    <div class="cm-stat-card__value">@_globalUsage</div>
    <div class="cm-stat-card__label">Requests / min</div>
</div>
<div class="cm-stat-card cm-stat-card--clickable" @onclick='() => Nav.NavigateTo("/allocations")'>
    <div class="cm-stat-card__arrow">
        <RadzenIcon Icon="north_east" />
    </div>
    <div class="cm-stat-card__value">@(_acquisitionPct)%</div>
    <div class="cm-stat-card__label">Pool Acquisition</div>
</div>
```

Also inject `NavigationManager Nav` (it is not currently injected in Dashboard.razor — add `@inject NavigationManager Nav` at the top).

### 2. Add arrow and clickable CSS

Edit [ClientManager.AdminUI/wwwroot/css/layout.css](ClientManager.AdminUI/wwwroot/css/layout.css).

Add after the existing `.cm-stat-card__label` block:

```css
/* Stat card clickable variant */
.cm-stat-card--clickable {
    cursor: pointer;
    position: relative;
}

.cm-stat-card__arrow {
    position: absolute;
    top: var(--space-sm);
    right: var(--space-sm);
    width: 28px;
    height: 28px;
    border-radius: 50%;
    background: rgba(0, 0, 0, 0.06);
    display: flex;
    align-items: center;
    justify-content: center;
    opacity: 0.5;
    transition: opacity 0.15s ease, background 0.15s ease;
}

.cm-stat-card__arrow .rzi {
    font-size: 16px;
}

.cm-stat-card--clickable:hover .cm-stat-card__arrow {
    opacity: 1;
    background: rgba(0, 0, 0, 0.1);
}

/* Arrow on primary card uses lighter colors */
.cm-stat-card--primary .cm-stat-card__arrow {
    background: rgba(255, 255, 255, 0.2);
    color: #ffffff;
}

.cm-stat-card--primary:hover .cm-stat-card__arrow {
    background: rgba(255, 255, 255, 0.35);
}
```

### 3. Add NavigationManager injection to Dashboard.razor

At the top of [Dashboard.razor](ClientManager.AdminUI/Components/Pages/Dashboard.razor), add:

```razor
@inject NavigationManager Nav
```

alongside the existing `@inject` directives.

## Verification

- Solution compiles without errors
- Each stat card shows a small arrow icon in the top-right corner
- Clicking "Clients" card navigates to `/clients`
- Clicking "Services" card navigates to `/services`
- Clicking "Resource Pools" card navigates to `/resource-pools`
- Clicking "Requests / min" card navigates to `/monitor` (will 404 until step 2 is done — that's expected)
- Clicking "Pool Acquisition" card navigates to `/allocations`
- Arrow is subtle (50% opacity) at rest, fully visible on hover
- Arrow on the primary (first) card uses white-on-primary styling
- Cards still have hover elevation effect
