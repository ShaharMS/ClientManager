# Plan: Logarithmic Axis Scaling — Step 2: Settings Card

> **Status**: ✅ Completed
> **Prerequisite**: [logarithmic-scaling-1-foundation.md](logarithmic-scaling-1-foundation.md)
> **Next**: [logarithmic-scaling-3-chart-settings-component.md](logarithmic-scaling-3-chart-settings-component.md)
> **Parent**: [logarithmic-scaling-overview.md](logarithmic-scaling-overview.md)

## TL;DR

Add a new "Default Axis Scale" settings card to the Settings page, letting the user choose between Linear and Logarithmic as their default chart scale. Follows the exact same card pattern as the existing "Default Time Range" and "Default Polling Interval" cards.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Settings.razor](ClientManager.AdminUI/Components/Pages/Settings.razor):
- Each setting is wrapped in a `cm-settings__card` div
- Card header: `RadzenIcon` + div with `h3.cm-settings__card-title` + `p.cm-settings__card-desc`
- Control section: `cm-settings__time-range-select` div wrapping a `RadzenDropDown`
- `@code` block: field for selected value, `OnAfterRenderAsync` loads from prefs, change handler saves via `PreferencesService`

## Steps

### 1. Add the card markup to Settings.razor

In [ClientManager.AdminUI/Components/Pages/Settings.razor](ClientManager.AdminUI/Components/Pages/Settings.razor), add a new card after the "Default Polling Interval" card:

```razor
<div class="cm-settings__card">
    <div class="cm-settings__card-header">
        <RadzenIcon Icon="show_chart" class="cm-settings__card-icon" />
        <div>
            <h3 class="cm-settings__card-title">Default Axis Scale</h3>
            <p class="cm-settings__card-desc">Choose how chart Y-axes scale values. Logarithmic makes small values more visible.</p>
        </div>
    </div>
    <div class="cm-settings__time-range-select">
        <RadzenDropDown TValue="string"
                        Data="@_axisScaleOptions"
                        TextProperty="Label"
                        ValueProperty="Value"
                        @bind-Value="_selectedAxisScale"
                        Change="@(args => OnAxisScaleChanged((string)args))"
                        Style="width: 100%; max-width: 280px;" />
    </div>
</div>
```

### 2. Add fields and options to the @code block

Add the following to the `@code` block in Settings.razor:

```csharp
private string _selectedAxisScale = "Linear";

private static readonly List<DropdownOption> _axisScaleOptions = new()
{
    new("Linear", "Linear"),
    new("Logarithmic", "Logarithmic")
};

private record DropdownOption(string Value, string Label);
```

### 3. Initialize from preferences

In the `OnAfterRenderAsync` method, after loading `_selectedPollingInterval`, add:

```csharp
_selectedAxisScale = prefs.DefaultAxisScale;
```

### 4. Add change handler

Add the change handler method:

```csharp
private async Task OnAxisScaleChanged(string value)
{
    _selectedAxisScale = value;
    var prefs = await PreferencesService.GetPreferencesAsync();
    prefs.DefaultAxisScale = value;
    await PreferencesService.SavePreferencesAsync(prefs);
}
```

## Verification

- `dotnet build ClientManager.AdminUI/ClientManager.AdminUI.csproj` compiles without errors
- **UI: Navigate to Settings (`/settings`) — verify a new "Default Axis Scale" card appears below the polling interval card**
- **UI: The dropdown shows two options: "Linear" and "Logarithmic"**
- **UI: Select "Logarithmic", refresh the page — verify the selection persists (loaded from localStorage)**
- **UI: Navigate to Dashboard (`/`) and back to Settings — verify no regressions on other cards**
