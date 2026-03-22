# Plan: Time Range Filtering & Settings — Step 4: Time Range Selector Component

> **Status**: ✅ Completed (redesigned from pills to dropdown)
> **Prerequisite**: [time-range-settings-3-api-updates.md](time-range-settings-3-api-updates.md)
> **Next**: [time-range-settings-5-page-integration.md](time-range-settings-5-page-integration.md)
> **Parent**: [time-range-settings-overview.md](time-range-settings-overview.md)

## TL;DR

Create a reusable `TimeRangeSelector.razor` component that displays pill-style buttons grouped by Minutes / Hours / Days, matching the Metric dashboard reference design's chart filter aesthetic. It reads the user's default time range from `UserPreferencesService` on init and emits the selected `TimeRangePreset` via `EventCallback`. The CSS for the pill buttons is already defined in `settings.css` (`.cm-time-pill`).

## Reference Pattern

**Design Reference**: [Dribbble Metric Dashboard Video](https://cdn.dribbble.com/userupload/42834013/file/original-333c4f78536a41262709503fae3c7342.mp4)

The Metric design shows a compact row of filter buttons above the chart area. The buttons are small, rounded pills with a filled active state. In the reference, they appear as a horizontal strip — e.g., "Monthly | Weekly | Daily" — styled as tabs/pills with one highlighted.

Our version groups pills by time unit (Minutes | Hours | Days) with a subtle label for each group, in a single horizontal row that fits in the chart header's filter area.

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](../../ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- The filter area is `<div class="cm-dashboard__filters">` inside `cm-dashboard__chart-header`
- Currently contains 3 `RadzenDropDown` components
- The TimeRangeSelector will be placed as the first child, before the existing dropdowns

In [ClientManager.AdminUI/wwwroot/css/settings.css](../../ClientManager.AdminUI/wwwroot/css/settings.css):
- `.cm-time-pill` and `.cm-time-pill--active` classes already defined in Step 2
- Reuse these same classes in the component

## Steps

### 1. Create the `TimeRangeSelector` component

Create `ClientManager.AdminUI/Components/TimeRangeSelector.razor`:

```razor
@inject UserPreferencesService PreferencesService

<div class="cm-time-range-selector">
    @foreach (var group in TimeRangePreset.All.GroupBy(p => p.Group))
    {
        <div class="cm-time-range-selector__group">
            <span class="cm-time-range-selector__group-label">@group.Key</span>
            @foreach (var preset in group)
            {
                <button class="cm-time-pill @(preset.Key == _selectedKey ? "cm-time-pill--active" : "")"
                        @onclick="() => SelectPreset(preset)">
                    @preset.Label
                </button>
            }
        </div>
    }
</div>

@code {
    [Parameter] public EventCallback<TimeRangePreset> OnRangeChanged { get; set; }
    [Parameter] public string? InitialKey { get; set; }

    private string _selectedKey = "1h";
    private bool _initialized;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_initialized)
        {
            _initialized = true;

            if (InitialKey is not null)
            {
                _selectedKey = InitialKey;
            }
            else
            {
                var defaultPreset = await PreferencesService.GetDefaultTimeRangeAsync();
                _selectedKey = defaultPreset.Key;
            }

            var preset = TimeRangePreset.FindByKey(_selectedKey) ?? TimeRangePreset.Default;
            await OnRangeChanged.InvokeAsync(preset);
            StateHasChanged();
        }
    }

    private async Task SelectPreset(TimeRangePreset preset)
    {
        _selectedKey = preset.Key;
        await OnRangeChanged.InvokeAsync(preset);
    }
}
```

### 2. Add component CSS

Add to `ClientManager.AdminUI/wwwroot/css/settings.css` (or create a separate `time-range-selector.css` if preferred — but since the pill styles are already there, adding the layout classes to the same file is cleaner):

```css
/* Time Range Selector — inline component for chart headers */
.cm-time-range-selector {
    display: flex;
    align-items: center;
    gap: var(--space-sm);
    flex-wrap: wrap;
}

.cm-time-range-selector__group {
    display: flex;
    align-items: center;
    gap: 3px;
}

.cm-time-range-selector__group-label {
    font-size: 10px;
    font-weight: 600;
    color: var(--color-text-muted);
    text-transform: uppercase;
    letter-spacing: 0.05em;
    margin-right: 2px;
}

/* Add a subtle separator between groups */
.cm-time-range-selector__group + .cm-time-range-selector__group {
    padding-left: var(--space-sm);
    border-left: 1px solid var(--color-border);
}
```

### 3. Move filter area to wrap properly

On the Dashboard, the chart header filter area will need to accommodate both the time range selector and the existing dropdowns. The time range selector should appear on its own line above the existing filter dropdowns if needed. This may require minor CSS tweaks to `cm-dashboard__filters` to allow wrapping:

In `ClientManager.AdminUI/wwwroot/css/dashboard.css`, update:

```css
.cm-dashboard__filters {
    display: flex;
    gap: var(--space-sm);
    align-items: center;
    flex-wrap: wrap;
}
```

This is already close to what exists. Just ensure `flex-wrap: wrap` is present.

## Verification

- Project compiles without errors.
- The `TimeRangeSelector` component renders 12 pill buttons across 3 groups.
- Clicking a pill invokes `OnRangeChanged` with the correct `TimeRangePreset`.
- The component loads the user's default time range from preferences on first render.
- **UI: The component will be wired into pages in Step 5 — for now, verify it compiles and can be placed in any page without errors.**
