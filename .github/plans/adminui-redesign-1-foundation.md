# Plan: AdminUI Redesign — Step 1: Foundation & Theme System

> **Status**: ✅ Completed
> **Prerequisite**: None — this is the first step.
> **Next**: [adminui-redesign-2-layout-nav.md](adminui-redesign-2-layout-nav.md)
> **Parent**: [adminui-redesign-overview.md](adminui-redesign-overview.md)

## TL;DR

Install Radzen.Blazor Community NuGet package, configure it in `Program.cs`, set up a CSS custom-property-based theme system with an Indigo/Purple palette, and remove/replace the default Bootstrap-heavy styling.

## Reference Pattern

**Design Reference**: [Dribbble Metric Dashboard Video](https://cdn.dribbble.com/userupload/42834013/file/original-333c4f78536a41262709503fae3c7342.mp4)

Key visual properties from the reference:
- Background: very light gray (`#f8f9fc` or similar)
- Cards: white with soft box-shadows, rounded corners (~12px)
- Primary color: indigo/purple (`#6366f1` range)
- Text: dark gray (`#1e293b`), secondary text lighter
- Sidebar: white background, not dark

In [ClientManager.AdminUI/wwwroot/app.css](../../ClientManager.AdminUI/wwwroot/app.css):
- Currently minimal Bootstrap overrides
- This is where we'll import the theme variables

In [ClientManager.AdminUI/Program.cs](../../ClientManager.AdminUI/Program.cs):
- Razor components already configured with Interactive Server
- Radzen services need to be registered here

## Steps

### 1. Install Radzen.Blazor NuGet package

Add the Radzen.Blazor Community package to the AdminUI project.

```bash
dotnet add ClientManager.AdminUI/ClientManager.AdminUI.csproj package Radzen.Blazor
```

### 2. Register Radzen services in `Program.cs`

In `ClientManager.AdminUI/Program.cs`, add Radzen service registration:

```csharp
using Radzen;

// After existing service registrations:
builder.Services.AddRadzenComponents();
```

### 3. Add Radzen imports to `_Imports.razor`

In `ClientManager.AdminUI/Components/_Imports.razor`, add:

```razor
@using Radzen
@using Radzen.Blazor
```

### 4. Add Radzen CSS and JS references to `App.razor`

In `ClientManager.AdminUI/Components/App.razor`, add to the `<head>`:

```html
<link rel="stylesheet" href="_content/Radzen.Blazor/css/material-base.css">
```

And before the closing `</body>`:

```html
<script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
```

### 5. Add `RadzenComponents` to `MainLayout.razor`

In `ClientManager.AdminUI/Components/Layout/MainLayout.razor`, add inside the layout:

```razor
<RadzenComponents />
```

### 6. Create the CSS theme variables file

Create `ClientManager.AdminUI/wwwroot/css/theme.css` with the full variable-based theme:

```css
:root {
    /* Primary palette — Indigo/Purple */
    --color-primary: #6366f1;
    --color-primary-light: #818cf8;
    --color-primary-lighter: #c7d2fe;
    --color-primary-lightest: #eef2ff;
    --color-primary-dark: #4f46e5;

    /* Accent */
    --color-accent: #f59e0b;
    --color-accent-light: #fbbf24;

    /* Semantic */
    --color-success: #22c55e;
    --color-warning: #f59e0b;
    --color-danger: #ef4444;
    --color-info: #3b82f6;

    /* Neutrals */
    --color-bg: #f8f9fc;
    --color-bg-card: #ffffff;
    --color-bg-sidebar: #ffffff;
    --color-text-primary: #1e293b;
    --color-text-secondary: #64748b;
    --color-text-muted: #94a3b8;
    --color-border: #e2e8f0;

    /* Shadows */
    --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.05);
    --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.07), 0 2px 4px -2px rgba(0, 0, 0, 0.05);
    --shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.08), 0 4px 6px -4px rgba(0, 0, 0, 0.04);

    /* Radii */
    --radius-sm: 8px;
    --radius-md: 12px;
    --radius-lg: 16px;

    /* Spacing */
    --space-xs: 0.25rem;
    --space-sm: 0.5rem;
    --space-md: 1rem;
    --space-lg: 1.5rem;
    --space-xl: 2rem;

    /* Typography */
    --font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    --font-size-xs: 0.75rem;
    --font-size-sm: 0.875rem;
    --font-size-md: 1rem;
    --font-size-lg: 1.25rem;
    --font-size-xl: 1.5rem;
    --font-size-2xl: 2rem;

    /* Sidebar */
    --sidebar-width: 250px;
    --sidebar-collapsed-width: 70px;
}
```

### 7. Create base layout styles file

Create `ClientManager.AdminUI/wwwroot/css/layout.css` with global layout overrides:

```css
/* Import Google Fonts - Inter */
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap');

html, body {
    font-family: var(--font-family);
    background-color: var(--color-bg);
    color: var(--color-text-primary);
    margin: 0;
    padding: 0;
}

/* Card base style — used throughout the app */
.cm-card {
    background: var(--color-bg-card);
    border-radius: var(--radius-md);
    box-shadow: var(--shadow-md);
    border: 1px solid var(--color-border);
    padding: var(--space-lg);
}

/* Stat card variant */
.cm-stat-card {
    background: var(--color-bg-card);
    border-radius: var(--radius-md);
    box-shadow: var(--shadow-sm);
    border: 1px solid var(--color-border);
    padding: var(--space-lg);
    transition: box-shadow 0.2s ease, transform 0.2s ease;
}

.cm-stat-card:hover {
    box-shadow: var(--shadow-md);
    transform: translateY(-2px);
}

.cm-stat-card--primary {
    background: var(--color-primary);
    color: #ffffff;
    border-color: var(--color-primary);
}

.cm-stat-card--primary .cm-stat-card__label {
    color: rgba(255, 255, 255, 0.8);
}

.cm-stat-card__value {
    font-size: var(--font-size-2xl);
    font-weight: 700;
    line-height: 1.2;
}

.cm-stat-card__label {
    font-size: var(--font-size-sm);
    color: var(--color-text-secondary);
    margin-top: var(--space-xs);
}

/* Page header */
.cm-page-header {
    margin-bottom: var(--space-xl);
}

.cm-page-header h1 {
    font-size: var(--font-size-xl);
    font-weight: 700;
    color: var(--color-text-primary);
    margin: 0;
}

.cm-page-header p {
    font-size: var(--font-size-sm);
    color: var(--color-text-secondary);
    margin: var(--space-xs) 0 0 0;
}
```

### 8. Reference the new CSS files in `App.razor`

In `ClientManager.AdminUI/Components/App.razor` `<head>`, add (before app.css):

```html
<link rel="stylesheet" href="css/theme.css">
<link rel="stylesheet" href="css/layout.css">
```

### 9. Clean up `app.css`

In `ClientManager.AdminUI/wwwroot/app.css`, remove or comment out the old Bootstrap-centric font-family and color rules that conflict with the new theme. Keep only Blazor-specific styles (validation, error boundary).

## Verification

- `dotnet build ClientManager.AdminUI/ClientManager.AdminUI.csproj` compiles without errors
- Radzen.Blazor package is listed in the .csproj
- `theme.css` and `layout.css` are referenced in `App.razor`
- Radzen services are registered in `Program.cs`
- `_Imports.razor` includes `@using Radzen` and `@using Radzen.Blazor`
- `RadzenComponents` is rendered in the layout

### Required: Browser Verification

Before marking this step complete, the implementer **must**:
1. Start the API project (`dotnet run --project ClientManager.Api`) in a background terminal and confirm it is listening.
2. Start the AdminUI project (`dotnet run --project ClientManager.AdminUI`) in a background terminal and confirm it is listening.
3. Open the AdminUI URL in the shared browser (using `open_browser_page`).
4. Take a screenshot and verify:
   - The page loads without errors
   - No CSS/JS console errors
   - The Radzen theme CSS is being applied (check that `material-base.css` loaded)
5. Share the screenshot with the user for sign-off before proceeding to the next step.
