# Plan: Time Range Filtering & Settings — Step 2: Settings Page

> **Status**: ✅ Completed
> **Prerequisite**: [time-range-settings-1-foundation.md](time-range-settings-1-foundation.md)
> **Next**: [time-range-settings-3-api-updates.md](time-range-settings-3-api-updates.md)
> **Parent**: [time-range-settings-overview.md](time-range-settings-overview.md)

## TL;DR

Create the `/settings` page with a dark/light mode toggle and a default time range picker, and wire the sidebar "Settings" link to navigate to it. The page uses `UserPreferencesService` to read/write localStorage-backed preferences and applies theme changes immediately.

## Reference Pattern

In [ClientManager.AdminUI/Components/Pages/Dashboard.razor](../../ClientManager.AdminUI/Components/Pages/Dashboard.razor):
- Uses `@page "/"` directive and `@inject` for services
- Follows page header pattern: `<div class="cm-page-header"><h1>...</h1><p>...</p></div>`
- Uses Radzen components wrapped in card-style containers

In [ClientManager.AdminUI/Components/Pages/ResourcePools/ResourcePoolList.razor](../../ClientManager.AdminUI/Components/Pages/ResourcePools/ResourcePoolList.razor):
- Example of a list page with the standard page header pattern
- Uses `cm-list-page__table-card` for card containers

In [ClientManager.AdminUI/Components/Layout/NavMenu.razor](../../ClientManager.AdminUI/Components/Layout/NavMenu.razor):
- Settings link is currently `<a href="#">` — needs to become `<NavLink href="settings">`

## Steps

### 1. Create the Settings page

Create `ClientManager.AdminUI/Components/Pages/Settings.razor`:

```razor
@page "/settings"
@inject UserPreferencesService PreferencesService

<div class="cm-page-header">
    <h1>Settings</h1>
    <p>Customize your dashboard experience.</p>
</div>

@if (_loading)
{
    <RadzenProgressBarCircular ShowValue="false" Mode="ProgressBarMode.Indeterminate" />
}
else
{
    <div class="cm-settings">
        <div class="cm-settings__card">
            <div class="cm-settings__card-header">
                <RadzenIcon Icon="palette" class="cm-settings__card-icon" />
                <div>
                    <h3 class="cm-settings__card-title">Appearance</h3>
                    <p class="cm-settings__card-desc">Choose between light and dark mode.</p>
                </div>
            </div>
            <div class="cm-settings__option">
                <span class="cm-settings__option-label">Dark Mode</span>
                <RadzenSwitch @bind-Value="_isDarkMode" Change="@OnThemeChanged" />
            </div>
        </div>

        <div class="cm-settings__card">
            <div class="cm-settings__card-header">
                <RadzenIcon Icon="schedule" class="cm-settings__card-icon" />
                <div>
                    <h3 class="cm-settings__card-title">Default Time Range</h3>
                    <p class="cm-settings__card-desc">Used by charts when no specific range is selected.</p>
                </div>
            </div>
            <div class="cm-settings__time-groups">
                @foreach (var group in TimeRangePreset.All.GroupBy(p => p.Group))
                {
                    <div class="cm-settings__time-group">
                        <span class="cm-settings__time-group-label">@group.Key</span>
                        <div class="cm-settings__time-pills">
                            @foreach (var preset in group)
                            {
                                <button class="cm-time-pill @(preset.Key == _selectedTimeRange ? "cm-time-pill--active" : "")"
                                        @onclick="() => OnTimeRangeChanged(preset.Key)">
                                    @preset.Label
                                </button>
                            }
                        </div>
                    </div>
                }
            </div>
        </div>
    </div>
}

@code {
    private bool _loading = true;
    private bool _isDarkMode;
    private string _selectedTimeRange = "1h";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var prefs = await PreferencesService.GetPreferencesAsync();
            _isDarkMode = prefs.Theme == "dark";
            _selectedTimeRange = prefs.DefaultTimeRange;
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task OnThemeChanged(bool value)
    {
        var prefs = await PreferencesService.GetPreferencesAsync();
        prefs.Theme = value ? "dark" : "light";
        await PreferencesService.SavePreferencesAsync(prefs);
    }

    private async Task OnTimeRangeChanged(string key)
    {
        _selectedTimeRange = key;
        var prefs = await PreferencesService.GetPreferencesAsync();
        prefs.DefaultTimeRange = key;
        await PreferencesService.SavePreferencesAsync(prefs);
    }
}
```

### 2. Create settings page CSS

Create `ClientManager.AdminUI/wwwroot/css/settings.css`:

```css
.cm-settings {
    display: flex;
    flex-direction: column;
    gap: var(--space-lg);
    max-width: 640px;
}

.cm-settings__card {
    background: var(--color-bg-card);
    border-radius: var(--radius-md);
    box-shadow: var(--shadow-sm);
    border: 1px solid var(--color-border);
    padding: var(--space-lg);
}

.cm-settings__card-header {
    display: flex;
    align-items: flex-start;
    gap: var(--space-md);
    margin-bottom: var(--space-lg);
}

.cm-settings__card-icon {
    font-size: 24px;
    color: var(--color-primary);
    margin-top: 2px;
}

.cm-settings__card-title {
    font-size: var(--font-size-md);
    font-weight: 600;
    color: var(--color-text-primary);
    margin: 0;
}

.cm-settings__card-desc {
    font-size: var(--font-size-sm);
    color: var(--color-text-secondary);
    margin: var(--space-xs) 0 0 0;
}

.cm-settings__option {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: var(--space-sm) 0;
}

.cm-settings__option-label {
    font-size: var(--font-size-sm);
    font-weight: 500;
    color: var(--color-text-primary);
}

.cm-settings__time-groups {
    display: flex;
    flex-direction: column;
    gap: var(--space-md);
}

.cm-settings__time-group {
    display: flex;
    align-items: center;
    gap: var(--space-md);
}

.cm-settings__time-group-label {
    font-size: var(--font-size-xs);
    font-weight: 600;
    color: var(--color-text-muted);
    text-transform: uppercase;
    letter-spacing: 0.05em;
    min-width: 60px;
}

.cm-settings__time-pills {
    display: flex;
    gap: var(--space-xs);
    flex-wrap: wrap;
}

/* Pill button — reused by TimeRangeSelector component later */
.cm-time-pill {
    padding: 0.3rem 0.75rem;
    border-radius: 999px;
    border: 1px solid var(--color-border);
    background: var(--color-bg-card);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: 500;
    cursor: pointer;
    transition: all 0.15s ease;
}

.cm-time-pill:hover {
    border-color: var(--color-primary-light);
    color: var(--color-primary);
    background: var(--color-primary-lightest);
}

.cm-time-pill--active {
    background: var(--color-primary);
    color: #ffffff;
    border-color: var(--color-primary);
}

.cm-time-pill--active:hover {
    background: var(--color-primary-dark);
    border-color: var(--color-primary-dark);
    color: #ffffff;
}
```

### 3. Reference the settings CSS in `App.razor`

In `ClientManager.AdminUI/Components/App.razor`, add after the other CSS links:

```html
<link rel="stylesheet" href="css/settings.css">
```

### 4. Wire the sidebar Settings link

In `ClientManager.AdminUI/Components/Layout/NavMenu.razor`, replace the current dead settings link:

```razor
<!-- Before -->
<a href="#" class="nav-link">
    <RadzenIcon Icon="settings" class="cm-sidebar__nav-icon" />
    Settings
</a>

<!-- After -->
<NavLink class="nav-link" href="settings">
    <RadzenIcon Icon="settings" class="cm-sidebar__nav-icon" />
    Settings
</NavLink>
```

## Verification

- Project compiles without errors.
- **UI: Click "Settings" in the sidebar — navigates to `/settings` page with active sidebar highlight.**
- **UI: The Settings page shows two cards: Appearance (with dark mode toggle) and Default Time Range (with pill buttons grouped by Minutes/Hours/Days).**
- **UI: Toggle dark mode on — the entire app immediately switches to a dark color scheme. Toggle off — returns to light.**
- **UI: Click a different time range pill (e.g., "30m") — it highlights as active. Reload the page — the selection persists.**
- **UI: Take a screenshot in both light and dark modes to verify visual consistency.**
