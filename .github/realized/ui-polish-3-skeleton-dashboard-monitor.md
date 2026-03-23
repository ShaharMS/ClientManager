# Plan: UI Polish — Step 3: Skeleton Loaders on Dashboard, Monitor & Allocations

> **Status**: ✅ Completed
> **Prerequisite**: [ui-polish-2-skeleton-crud-pages.md](ui-polish-2-skeleton-crud-pages.md)
> **Next**: [ui-polish-4-deterministic-colors.md](ui-polish-4-deterministic-colors.md)
> **Parent**: [ui-polish-overview.md](ui-polish-overview.md)

## TL;DR

Replace the `RadzenProgressBarCircular` full-page spinner on Dashboard, the `RadzenProgressBarCircular` chart-area fallback on Monitor and Allocations, and the indeterminate `RadzenProgressBar` top-bar on Monitor and Allocations — all with contextual skeleton components that match the actual layout rendered after data loads.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](../../ClientManager.AdminUI/Components/Pages/Dashboard.razor):
```razor
@if (_loading)
{
    <RadzenProgressBarCircular ShowValue="false" Mode="ProgressBarMode.Indeterminate" />
}
```
The loaded state shows: stat cards row → two chart cards → a table. The skeleton should mirror this layout.

In [ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor](../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor):
- Top-of-page indeterminate bar:
  ```razor
  @if (_loading)
  {
      <RadzenProgressBar Value="100" ShowValue="false" Mode="ProgressBarMode.Indeterminate" ... />
  }
  ```
- Chart empty state fallback:
  ```razor
  @if (_targetCharts.Count > 0)
  { ... }
  else
  {
      <div style="height: 340px; ...">
          <RadzenProgressBarCircular ShowValue="false" Mode="ProgressBarMode.Indeterminate" />
      </div>
  }
  ```

The same pattern exists in [ActiveAllocations.razor](../../ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor).

## Steps

### 1. Replace Dashboard full-page loading spinner

In `ClientManager.AdminUI/Components/Pages/Dashboard.razor`, replace:

```razor
@if (_loading)
{
    <RadzenProgressBarCircular ShowValue="false" Mode="ProgressBarMode.Indeterminate" />
}
```

With a skeleton that mirrors the actual Dashboard layout:

```razor
@if (_loading)
{
    <StatCardsSkeleton Count="5" />
    <div class="cm-dashboard__charts" style="margin-top: var(--space-xl);">
        <ChartSkeleton />
        <ChartSkeleton />
    </div>
    <TableSkeleton RowCount="6" />
}
```

### 2. Replace Monitor top-of-page indeterminate bar

In `ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor`, replace:

```razor
@if (_loading)
{
    <RadzenProgressBar Value="100" ShowValue="false" Mode="ProgressBarMode.Indeterminate"
        Style="height: 3px; margin-bottom: var(--space-sm);" />
}
```

With:
```razor
@if (_loading)
{
    <SkeletonBlock CssClass="cm-skeleton--text" Style="height: 3px; margin-bottom: var(--space-sm);" />
}
```

### 3. Replace Monitor chart-area empty-state spinner

In the same file, replace the chart fallback:

```razor
else
{
    <div style="height: 340px; display: flex; align-items: center; justify-content: center;">
        <RadzenProgressBarCircular ShowValue="false" Mode="ProgressBarMode.Indeterminate" />
    </div>
}
```

With:
```razor
else
{
    <ChartSkeleton />
}
```

### 4. Replace ActiveAllocations top-of-page indeterminate bar

In `ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor`, replace:

```razor
@if (_loading)
{
    <RadzenProgressBar Value="100" ShowValue="false" Mode="ProgressBarMode.Indeterminate"
        Style="height: 3px; margin-bottom: var(--space-sm);" />
}
```

With:
```razor
@if (_loading)
{
    <SkeletonBlock CssClass="cm-skeleton--text" Style="height: 3px; margin-bottom: var(--space-sm);" />
}
```

### 5. Replace ActiveAllocations chart-area empty-state spinner

Same file, replace the chart fallback:

```razor
else
{
    <div style="height: 340px; display: flex; align-items: center; justify-content: center;">
        <RadzenProgressBarCircular ShowValue="false" Mode="ProgressBarMode.Indeterminate" />
    </div>
}
```

With:
```razor
else
{
    <ChartSkeleton />
}
```

## Verification

- Project compiles without errors
- **UI: Navigate to `/` (Dashboard) — verify stat card skeletons + chart skeletons + table skeleton appear during initial load, then fade into real content**
- **UI: Navigate to `/monitor` — verify the top shimmer bar appears during refresh, and the chart area shows a chart-shaped skeleton when no data has loaded yet**
- **UI: Navigate to `/allocations` — same verification as Monitor**
- **UI: Confirm no `RadzenProgressBarCircular` or indeterminate `RadzenProgressBar` components remain anywhere in the loading paths (they should only remain as data visualisation utilization bars inside table columns)**
