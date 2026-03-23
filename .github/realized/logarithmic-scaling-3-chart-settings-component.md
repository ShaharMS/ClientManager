# Plan: Logarithmic Axis Scaling — Step 3: Chart Settings Dropdown Component

> **Status**: ✅ Completed
> **Prerequisite**: [logarithmic-scaling-2-settings-card.md](logarithmic-scaling-2-settings-card.md)
> **Next**: [logarithmic-scaling-4-chart-integration.md](logarithmic-scaling-4-chart-integration.md)
> **Parent**: [logarithmic-scaling-overview.md](logarithmic-scaling-overview.md)

## TL;DR

Create a `ChartSettingsDropdown` component — a single gear-icon pill chip that opens a flyout panel. The flyout contains the time range selector, polling interval selector, and a new axis scale selector, all rendered as labeled chip rows. This replaces the separate `PollingIntervalSelector` + `TimeRangeSelector` chips currently placed side-by-side in chart headers.

## Reference Pattern

In [ClientManager.AdminUI/Components/TimeRangeSelector.razor](ClientManager.AdminUI/Components/TimeRangeSelector.razor):
- Pill trigger button with icon + label
- Overlay div to close on click-outside
- Menu div positioned absolutely below the trigger
- Items with active state styling
- Loads default from `UserPreferencesService` in `OnAfterRenderAsync`
- Fires `EventCallback` on selection

In [ClientManager.AdminUI/Components/PollingIntervalSelector.razor](ClientManager.AdminUI/Components/PollingIntervalSelector.razor):
- Same pattern as above, flat list instead of grouped

CSS patterns in the existing dropdown styles (`cm-time-range-dropdown`, `cm-polling-interval-dropdown`):
- Pill-shaped trigger: `border-radius: 999px`, inline-flex, gap 4px, padding 4px 12px
- Menu: absolute positioning, card background, border, shadow, z-index 100
- Items: full-width buttons, hover/active states using `--color-primary-lightest`

## Steps

### 1. Create `ChartSettingsDropdown.razor`

Create `ClientManager.AdminUI/Components/ChartSettingsDropdown.razor`:

The component has a single pill trigger with a gear icon and "Settings" label. Clicking it opens a flyout panel. The flyout contains three sections, each with a label and the corresponding selector rendered inline as a sub-dropdown.

```razor
@inject UserPreferencesService PreferencesService

<div class="cm-chart-settings-dropdown">
    <button class="cm-chart-settings-dropdown__trigger" @onclick="ToggleFlyout" @onclick:stopPropagation="true">
        <span class="cm-chart-settings-dropdown__icon material-icons">tune</span>
        <span class="cm-chart-settings-dropdown__label">Settings</span>
    </button>
    @if (_isOpen)
    {
        <div class="cm-chart-settings-dropdown__overlay" @onclick="CloseFlyout"></div>
        <div class="cm-chart-settings-dropdown__panel">
            <!-- Time Range section -->
            <div class="cm-chart-settings-dropdown__section">
                <span class="cm-chart-settings-dropdown__section-label">Time Range</span>
                <div class="cm-chart-settings-dropdown__chips">
                    @foreach (var group in TimeRangePreset.All.GroupBy(p => p.Group))
                    {
                        @foreach (var preset in group)
                        {
                            <button class="cm-chart-settings-dropdown__chip @(preset.Key == _selectedTimeRangeKey ? "cm-chart-settings-dropdown__chip--active" : "")"
                                    @onclick="() => SelectTimeRange(preset)" @onclick:stopPropagation="true">
                                @preset.Label
                            </button>
                        }
                    }
                </div>
            </div>

            <!-- Polling Interval section -->
            <div class="cm-chart-settings-dropdown__section">
                <span class="cm-chart-settings-dropdown__section-label">Refresh Rate</span>
                <div class="cm-chart-settings-dropdown__chips">
                    @foreach (var preset in PollingIntervalPreset.All)
                    {
                        <button class="cm-chart-settings-dropdown__chip @(preset.Key == _selectedPollingKey ? "cm-chart-settings-dropdown__chip--active" : "")"
                                @onclick="() => SelectPollingInterval(preset)" @onclick:stopPropagation="true">
                            @preset.Label
                        </button>
                    }
                </div>
            </div>

            <!-- Axis Scale section -->
            <div class="cm-chart-settings-dropdown__section">
                <span class="cm-chart-settings-dropdown__section-label">Vertical Axis</span>
                <div class="cm-chart-settings-dropdown__chips">
                    <button class="cm-chart-settings-dropdown__chip @(_selectedAxisScale == AxisScaleType.Linear ? "cm-chart-settings-dropdown__chip--active" : "")"
                            @onclick="() => SelectAxisScale(AxisScaleType.Linear)" @onclick:stopPropagation="true">
                        Linear
                    </button>
                    <button class="cm-chart-settings-dropdown__chip @(_selectedAxisScale == AxisScaleType.Logarithmic ? "cm-chart-settings-dropdown__chip--active" : "")"
                            @onclick="() => SelectAxisScale(AxisScaleType.Logarithmic)" @onclick:stopPropagation="true">
                        Logarithmic
                    </button>
                </div>
            </div>
        </div>
    }
</div>
```

Parameters:

```csharp
@code {
    [Parameter] public EventCallback<TimeRangePreset> OnTimeRangeChanged { get; set; }
    [Parameter] public EventCallback<PollingIntervalPreset> OnPollingIntervalChanged { get; set; }
    [Parameter] public EventCallback<AxisScaleType> OnAxisScaleChanged { get; set; }
    [Parameter] public string? InitialTimeRangeKey { get; set; }

    private bool _isOpen;
    private bool _initialized;
    private string _selectedTimeRangeKey = "1h";
    private string _selectedPollingKey = "10s";
    private AxisScaleType _selectedAxisScale = AxisScaleType.Linear;
}
```

The component loads defaults from `UserPreferencesService` in `OnAfterRenderAsync` (same as `TimeRangeSelector` and `PollingIntervalSelector` do today), then fires all three `EventCallback`s once to set initial values in the parent.

### 2. Create CSS for the component

Create `ClientManager.AdminUI/wwwroot/css/chart-settings-dropdown.css`:

Key styles:
- `.cm-chart-settings-dropdown` — `position: relative; display: inline-block;`
- `.cm-chart-settings-dropdown__trigger` — same pill shape as `cm-time-range-dropdown__trigger` (border-radius 999px, inline-flex, etc.)
- `.cm-chart-settings-dropdown__overlay` — fixed full-screen transparent overlay (same as existing dropdowns)
- `.cm-chart-settings-dropdown__panel` — absolute positioned card (right: 0, top: calc(100% + 4px)), min-width ~320px, card bg/border/shadow, padding, z-index 100
- `.cm-chart-settings-dropdown__section` — margin-bottom spacing between sections
- `.cm-chart-settings-dropdown__section-label` — small label in `--color-text-secondary`, font-size `--font-size-xs`, font-weight 600, uppercase
- `.cm-chart-settings-dropdown__chips` — flex-wrap container, gap 4px
- `.cm-chart-settings-dropdown__chip` — same pill styling as existing dropdown triggers but smaller (padding 3px 10px, font-size --font-size-xs), border 1px solid --color-border
- `.cm-chart-settings-dropdown__chip--active` — `background: var(--color-primary-lightest); color: var(--color-primary); border-color: var(--color-primary); font-weight: 600;`

### 3. Link the CSS

In [ClientManager.AdminUI/Components/App.razor](ClientManager.AdminUI/Components/App.razor) (or equivalent layout), add a `<link>` for the new CSS file, following the pattern of existing stylesheet includes.

## Verification

- `dotnet build ClientManager.AdminUI/ClientManager.AdminUI.csproj` compiles without errors
- **UI: The component is not yet used on any page — this step only creates the component and CSS files. Verify build success.**
- **UI: Optionally, temporarily drop `<ChartSettingsDropdown />` on the Dashboard to test rendering — a "Settings" pill should appear, clicking it opens a flyout with three sections, each containing clickable chips**
- **UI: Take a screenshot of the flyout open state to confirm layout — chips should wrap neatly, active chip should be highlighted**
