# Plan: UI Polish — Step 1: Skeleton Foundation

> **Status**: ✅ Completed
> **Prerequisite**: None — this is the first step.
> **Next**: [ui-polish-2-skeleton-crud-pages.md](ui-polish-2-skeleton-crud-pages.md)
> **Parent**: [ui-polish-overview.md](ui-polish-overview.md)

## TL;DR

Create a reusable CSS skeleton shimmer animation and a `SkeletonBlock.razor` component that renders gray placeholder shapes with a smooth left-to-right white sweep. This is the building block used by all subsequent skeleton sub-plans.

## Reference Pattern

No existing skeleton in the codebase. The component follows the same pattern as other layout CSS files in [ClientManager.AdminUI/wwwroot/css/](../../ClientManager.AdminUI/wwwroot/css/):
- Dedicated CSS file per concern (like `dashboard.css`, `monitor.css`, `list-pages.css`)
- CSS custom properties from [ClientManager.AdminUI/wwwroot/css/theme.css](../../ClientManager.AdminUI/wwwroot/css/theme.css) for colors and spacing

## Steps

### 1. Create `skeleton.css`

Create `ClientManager.AdminUI/wwwroot/css/skeleton.css` with the shimmer animation and skeleton utility classes.

```css
/* Skeleton shimmer animation */
@keyframes cm-skeleton-shimmer {
    0% { background-position: -200% 0; }
    100% { background-position: 200% 0; }
}

.cm-skeleton {
    background: linear-gradient(
        90deg,
        #e2e8f0 25%,
        #f1f5f9 50%,
        #e2e8f0 75%
    );
    background-size: 200% 100%;
    animation: cm-skeleton-shimmer 1.5s ease-in-out infinite;
    border-radius: var(--radius-sm);
}

/* Pre-made skeleton shapes */
.cm-skeleton--text {
    height: 1rem;
    margin-bottom: 0.75rem;
    border-radius: 4px;
}

.cm-skeleton--text-short {
    width: 60%;
}

.cm-skeleton--heading {
    height: 1.5rem;
    width: 40%;
    margin-bottom: 1rem;
    border-radius: 4px;
}

.cm-skeleton--chart {
    height: 300px;
    border-radius: var(--radius-md);
}

.cm-skeleton--stat-card {
    height: 100px;
    border-radius: var(--radius-md);
}

.cm-skeleton--table-row {
    height: 2.5rem;
    margin-bottom: 0.5rem;
    border-radius: 4px;
}

.cm-skeleton--circle {
    border-radius: 50%;
}
```

### 2. Register `skeleton.css` in the HTML head

In `ClientManager.AdminUI/Components/App.razor` (or wherever CSS is linked — check the `<head>` section), add a `<link>` for `css/skeleton.css` alongside the other custom CSS files.

```html
<link rel="stylesheet" href="css/skeleton.css" />
```

### 3. Create `SkeletonBlock.razor` shared component

Create `ClientManager.AdminUI/Components/Shared/SkeletonBlock.razor`. This is a simple parameterized component:

```razor
@* Reusable skeleton placeholder block *@
<div class="cm-skeleton @CssClass" style="@Style"></div>

@code {
    [Parameter] public string CssClass { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
}
```

### 4. Create composite skeleton layouts as components

Create `ClientManager.AdminUI/Components/Shared/Skeletons/TableSkeleton.razor`:

```razor
@* Skeleton placeholder for a data table *@
<div class="cm-list-page__table-card">
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-lg);">
        <SkeletonBlock CssClass="cm-skeleton--text" Style="width: 280px;" />
        <SkeletonBlock CssClass="cm-skeleton--text" Style="width: 140px;" />
    </div>
    @for (var i = 0; i < RowCount; i++)
    {
        <SkeletonBlock CssClass="cm-skeleton--table-row" />
    }
</div>

@code {
    [Parameter] public int RowCount { get; set; } = 8;
}
```

Create `ClientManager.AdminUI/Components/Shared/Skeletons/ChartSkeleton.razor`:

```razor
@* Skeleton placeholder for a chart area *@
<div class="cm-dashboard__chart-card">
    <div style="margin-bottom: var(--space-md);">
        <SkeletonBlock CssClass="cm-skeleton--heading" />
    </div>
    <SkeletonBlock CssClass="cm-skeleton--chart" />
</div>

@code {
}
```

Create `ClientManager.AdminUI/Components/Shared/Skeletons/FormSkeleton.razor`:

```razor
@* Skeleton placeholder for an editor form *@
<div class="cm-editor" style="max-width: 800px;">
    <div class="cm-editor__card">
        <SkeletonBlock CssClass="cm-skeleton--heading" Style="margin-bottom: var(--space-md);" />
        @for (var i = 0; i < FieldCount; i++)
        {
            <div style="margin-bottom: var(--space-md);">
                <SkeletonBlock CssClass="cm-skeleton--text cm-skeleton--text-short" Style="height: 0.75rem; margin-bottom: 0.5rem;" />
                <SkeletonBlock CssClass="cm-skeleton--text" Style="height: 2.25rem;" />
            </div>
        }
    </div>
</div>

@code {
    [Parameter] public int FieldCount { get; set; } = 5;
}
```

Create `ClientManager.AdminUI/Components/Shared/Skeletons/StatCardsSkeleton.razor`:

```razor
@* Skeleton placeholder for the dashboard stat cards row *@
<div class="cm-dashboard__stats">
    @for (var i = 0; i < Count; i++)
    {
        <SkeletonBlock CssClass="cm-skeleton--stat-card" />
    }
</div>

@code {
    [Parameter] public int Count { get; set; } = 5;
}
```

## Verification

- Project compiles without errors (`dotnet build`)
- `skeleton.css` is loaded in the browser (check DevTools Network tab or Elements `<head>`)
- The shimmer animation renders a smooth gray-to-white sweep when `<SkeletonBlock />` is placed on any page
- **UI: Navigate to any page — confirm no visual regressions from adding the CSS file**
