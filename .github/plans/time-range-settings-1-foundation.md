# Plan: Time Range Filtering & Settings — Step 1: Foundation

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [time-range-settings-2-settings-page.md](time-range-settings-2-settings-page.md)
> **Parent**: [time-range-settings-overview.md](time-range-settings-overview.md)

## TL;DR

Define the `TimeRangePreset` model with all presets and auto-granularity mapping, create a dark theme CSS override file, add a JS interop module for localStorage access, and build a `UserPreferencesService` that reads/writes user preferences (theme, default time range) via `IJSRuntime`.

## Reference Pattern

In [ClientManager.AdminUI/wwwroot/css/theme.css](../../ClientManager.AdminUI/wwwroot/css/theme.css):
- All styling uses CSS custom properties (`:root` block)
- Dark mode will override these same variables under `html[data-theme="dark"]`

In [ClientManager.AdminUI/Services/StatisticsApiService.cs](../../ClientManager.AdminUI/Services/StatisticsApiService.cs):
- Existing services use constructor-injected `IHttpClientFactory` or `IJSRuntime`
- Registered as scoped in `Program.cs`

In [ClientManager.AdminUI/Program.cs](../../ClientManager.AdminUI/Program.cs):
- Services registered with `builder.Services.AddScoped<T>()`

## Steps

### 1. Create `TimeRangePreset` model

Create `ClientManager.AdminUI/Models/TimeRangePreset.cs`:

```csharp
namespace ClientManager.AdminUI.Models;

public record TimeRangePreset(string Key, string Label, string Group, TimeSpan Duration, string Granularity)
{
    public DateTime GetFrom() => DateTime.UtcNow - Duration;
    public DateTime GetTo() => DateTime.UtcNow;

    public static readonly List<TimeRangePreset> All = new()
    {
        new("1m",  "1m",  "Minutes", TimeSpan.FromMinutes(1),  "FiveMinute"),
        new("5m",  "5m",  "Minutes", TimeSpan.FromMinutes(5),  "FiveMinute"),
        new("15m", "15m", "Minutes", TimeSpan.FromMinutes(15), "FiveMinute"),
        new("30m", "30m", "Minutes", TimeSpan.FromMinutes(30), "FiveMinute"),
        new("1h",  "1h",  "Hours",   TimeSpan.FromHours(1),    "FiveMinute"),
        new("3h",  "3h",  "Hours",   TimeSpan.FromHours(3),    "FiveMinute"),
        new("6h",  "6h",  "Hours",   TimeSpan.FromHours(6),    "FiveMinute"),
        new("12h", "12h", "Hours",   TimeSpan.FromHours(12),   "Hour"),
        new("1d",  "1d",  "Days",    TimeSpan.FromDays(1),     "Hour"),
        new("7d",  "7d",  "Days",    TimeSpan.FromDays(7),     "Hour"),
        new("30d", "30d", "Days",    TimeSpan.FromDays(30),    "Day"),
        new("90d", "90d", "Days",    TimeSpan.FromDays(90),    "Day"),
    };

    public static readonly TimeRangePreset Default = All.First(p => p.Key == "1h");

    public static TimeRangePreset? FindByKey(string? key) =>
        key is null ? null : All.FirstOrDefault(p => p.Key == key);
}
```

### 2. Create `UserPreferences` model

Create `ClientManager.AdminUI/Models/UserPreferences.cs`:

```csharp
namespace ClientManager.AdminUI.Models;

public class UserPreferences
{
    public string Theme { get; set; } = "light";
    public string DefaultTimeRange { get; set; } = "1h";
}
```

### 3. Create JS interop module for localStorage

Create `ClientManager.AdminUI/wwwroot/js/preferences.js`:

```js
export function getPreferences() {
    const raw = localStorage.getItem("cm-preferences");
    return raw ? JSON.parse(raw) : null;
}

export function savePreferences(prefs) {
    localStorage.setItem("cm-preferences", JSON.stringify(prefs));
}

export function applyTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
}
```

### 4. Create `UserPreferencesService`

Create `ClientManager.AdminUI/Services/UserPreferencesService.cs`:

```csharp
using ClientManager.AdminUI.Models;
using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Services;

public class UserPreferencesService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private UserPreferences? _cached;

    public UserPreferencesService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public event Action? OnPreferencesChanged;

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/preferences.js");
        return _module;
    }

    public async Task<UserPreferences> GetPreferencesAsync()
    {
        if (_cached is not null) return _cached;

        var module = await GetModuleAsync();
        _cached = await module.InvokeAsync<UserPreferences?>("getPreferences")
                  ?? new UserPreferences();
        return _cached;
    }

    public async Task SavePreferencesAsync(UserPreferences preferences)
    {
        _cached = preferences;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("savePreferences", preferences);
        await module.InvokeVoidAsync("applyTheme", preferences.Theme);
        OnPreferencesChanged?.Invoke();
    }

    public async Task ApplyCurrentThemeAsync()
    {
        var prefs = await GetPreferencesAsync();
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("applyTheme", prefs.Theme);
    }

    public async Task<TimeRangePreset> GetDefaultTimeRangeAsync()
    {
        var prefs = await GetPreferencesAsync();
        return TimeRangePreset.FindByKey(prefs.DefaultTimeRange)
               ?? TimeRangePreset.Default;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
```

### 5. Create dark theme CSS

Create `ClientManager.AdminUI/wwwroot/css/theme-dark.css`:

```css
html[data-theme="dark"] {
    /* Primary palette — keep hues, adjust lightness */
    --color-primary: #818cf8;
    --color-primary-light: #a5b4fc;
    --color-primary-lighter: #3730a3;
    --color-primary-lightest: #1e1b4b;
    --color-primary-dark: #6366f1;

    /* Accent */
    --color-accent: #fbbf24;
    --color-accent-light: #fcd34d;

    /* Semantic */
    --color-success: #4ade80;
    --color-warning: #fbbf24;
    --color-danger: #f87171;
    --color-info: #60a5fa;

    /* Neutrals — inverted */
    --color-bg: #0f172a;
    --color-bg-card: #1e293b;
    --color-bg-sidebar: #1e293b;
    --color-text-primary: #f1f5f9;
    --color-text-secondary: #94a3b8;
    --color-text-muted: #64748b;
    --color-border: #334155;

    /* Shadows — darker, more subtle */
    --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.3);
    --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.4), 0 2px 4px -2px rgba(0, 0, 0, 0.3);
    --shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.5), 0 4px 6px -4px rgba(0, 0, 0, 0.3);
}

/* Override Radzen component backgrounds in dark mode */
html[data-theme="dark"] .rz-datatable,
html[data-theme="dark"] .rz-grid-table,
html[data-theme="dark"] .rz-data-grid {
    background: var(--color-bg-card);
    color: var(--color-text-primary);
}

html[data-theme="dark"] .rz-datatable .rz-cell,
html[data-theme="dark"] .rz-data-grid .rz-cell {
    border-color: var(--color-border);
}

html[data-theme="dark"] .rz-pager {
    background: var(--color-bg-card);
    color: var(--color-text-secondary);
}

html[data-theme="dark"] .rz-dropdown,
html[data-theme="dark"] .rz-multiselect,
html[data-theme="dark"] .rz-textbox {
    background: var(--color-bg);
    color: var(--color-text-primary);
    border-color: var(--color-border);
}

html[data-theme="dark"] .rz-dropdown-panel,
html[data-theme="dark"] .rz-multiselect-panel {
    background: var(--color-bg-card);
    color: var(--color-text-primary);
    border-color: var(--color-border);
}

html[data-theme="dark"] .rz-state-highlight {
    background: var(--color-primary);
    color: #ffffff;
}

html[data-theme="dark"] .rz-switch .rz-switch-circle {
    background: var(--color-bg-card);
}
```

### 6. Register service and add CSS/JS references

In `ClientManager.AdminUI/Program.cs`, add:

```csharp
builder.Services.AddScoped<UserPreferencesService>();
```

In `ClientManager.AdminUI/Components/App.razor`, add the dark theme CSS after `theme.css`:

```html
<link rel="stylesheet" href="css/theme-dark.css">
```

And add the JS module (as type="module") before the closing `</body>`:

```html
<!-- no script tag needed — the JS module is imported dynamically via IJSRuntime -->
```

### 7. Apply theme on app start

In `ClientManager.AdminUI/Components/Layout/MainLayout.razor`, add `OnAfterRenderAsync` to apply the stored theme on first render:

```razor
@inherits LayoutComponentBase
@inject UserPreferencesService PreferencesService

<NavMenu />
<div class="cm-main">
    @Body
</div>
<RadzenComponents />

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await PreferencesService.ApplyCurrentThemeAsync();
        }
    }
}
```

### 8. Add `Models` namespace to `_Imports.razor`

In `ClientManager.AdminUI/Components/_Imports.razor`, add:

```razor
@using ClientManager.AdminUI.Models
```

## Verification

- Project compiles without errors.
- `TimeRangePreset.All` contains all 12 presets with correct grouping and granularity mapping.
- `UserPreferencesService` can be injected (registered in `Program.cs`).
- `theme-dark.css` is referenced in `App.razor` and overrides CSS variables when `data-theme="dark"` is set.
- `preferences.js` is loadable as an ES module.
- **UI: Navigate to the Dashboard at `/` — verify the page loads without JS errors in the browser console.**
- **UI: Manually set `localStorage.setItem("cm-preferences", '{"Theme":"dark","DefaultTimeRange":"1h"}')` in the browser console, then reload — verify the page renders with a dark background.**
