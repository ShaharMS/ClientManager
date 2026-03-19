# Plan: AdminUI Redesign — Step 2: Layout & Navigation

> **Status**: ✅ Completed
> **Prerequisite**: [adminui-redesign-1-foundation.md](adminui-redesign-1-foundation.md)
> **Next**: [adminui-redesign-3-dashboard.md](adminui-redesign-3-dashboard.md)
> **Parent**: [adminui-redesign-overview.md](adminui-redesign-overview.md)

## TL;DR

Rebuild `MainLayout.razor` and `NavMenu.razor` to match the Metric dashboard's clean white sidebar with icon navigation, colored active-state pill, app branding at top, and settings/help at bottom. The main content area gets a light gray background with proper spacing.

## Reference Pattern

**Design Reference**: [Dribbble Metric Dashboard Video](https://cdn.dribbble.com/userupload/42834013/file/original-333c4f78536a41262709503fae3c7342.mp4)

Key sidebar elements from the reference:
- White background sidebar (not dark)
- App logo/name at top ("Metric" with grid icon)
- Nav items are vertical with icons + text labels
- Active item has a filled indigo/purple rounded pill background
- Inactive items are gray text
- "Settings" and "Help and Support" pinned to the bottom of the sidebar
- Main content has light gray background (`#f8f9fc`)
- Top bar in main content area shows a welcome header and search

In [ClientManager.AdminUI/Components/Layout/MainLayout.razor](../../ClientManager.AdminUI/Components/Layout/MainLayout.razor):
- Currently a simple `d-flex` with `NavMenu` + `<main>` side by side

In [ClientManager.AdminUI/Components/Layout/NavMenu.razor](../../ClientManager.AdminUI/Components/Layout/NavMenu.razor):
- Currently a `bg-dark` nav with plain `NavLink` items, no icons

## Steps

### 1. Create sidebar CSS file

Create `ClientManager.AdminUI/wwwroot/css/sidebar.css`:

```css
.cm-sidebar {
    width: var(--sidebar-width);
    min-height: 100vh;
    background: var(--color-bg-sidebar);
    border-right: 1px solid var(--color-border);
    display: flex;
    flex-direction: column;
    padding: var(--space-lg) var(--space-md);
    position: fixed;
    top: 0;
    left: 0;
    z-index: 100;
}

.cm-sidebar__brand {
    display: flex;
    align-items: center;
    gap: var(--space-sm);
    padding: var(--space-sm) var(--space-sm);
    margin-bottom: var(--space-xl);
}

.cm-sidebar__brand-icon {
    width: 32px;
    height: 32px;
    background: var(--color-primary);
    border-radius: var(--radius-sm);
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    font-weight: 700;
    font-size: var(--font-size-sm);
}

.cm-sidebar__brand-name {
    font-size: var(--font-size-lg);
    font-weight: 700;
    color: var(--color-text-primary);
}

.cm-sidebar__nav {
    list-style: none;
    padding: 0;
    margin: 0;
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: var(--space-xs);
}

.cm-sidebar__nav-item a,
.cm-sidebar__nav-item .nav-link {
    display: flex;
    align-items: center;
    gap: var(--space-sm);
    padding: 0.6rem var(--space-md);
    border-radius: var(--radius-sm);
    color: var(--color-text-secondary);
    text-decoration: none;
    font-size: var(--font-size-sm);
    font-weight: 500;
    transition: all 0.15s ease;
}

.cm-sidebar__nav-item a:hover,
.cm-sidebar__nav-item .nav-link:hover {
    background: var(--color-primary-lightest);
    color: var(--color-primary);
}

.cm-sidebar__nav-item a.active,
.cm-sidebar__nav-item .nav-link.active {
    background: var(--color-primary);
    color: #ffffff;
}

.cm-sidebar__nav-icon {
    width: 20px;
    height: 20px;
    flex-shrink: 0;
}

.cm-sidebar__footer {
    margin-top: auto;
    padding-top: var(--space-md);
    border-top: 1px solid var(--color-border);
    display: flex;
    flex-direction: column;
    gap: var(--space-xs);
}

/* Main content area */
.cm-main {
    margin-left: var(--sidebar-width);
    flex: 1;
    min-height: 100vh;
    background: var(--color-bg);
    padding: var(--space-xl) var(--space-xl);
}
```

### 2. Reference sidebar CSS in `App.razor`

Add to `<head>` in `ClientManager.AdminUI/Components/App.razor`:

```html
<link rel="stylesheet" href="css/sidebar.css">
```

### 3. Rebuild `NavMenu.razor`

Replace the content of `ClientManager.AdminUI/Components/Layout/NavMenu.razor` with the Metric-style sidebar. Use Radzen icons or simple SVG icons for each nav item:

```razor
<aside class="cm-sidebar">
    <div class="cm-sidebar__brand">
        <div class="cm-sidebar__brand-icon">CM</div>
        <span class="cm-sidebar__brand-name">ClientManager</span>
    </div>

    <ul class="cm-sidebar__nav">
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
        <li class="cm-sidebar__nav-item">
            <NavLink class="nav-link" href="services">
                <RadzenIcon Icon="miscellaneous_services" class="cm-sidebar__nav-icon" />
                Services
            </NavLink>
        </li>
        <li class="cm-sidebar__nav-item">
            <NavLink class="nav-link" href="resource-pools">
                <RadzenIcon Icon="hub" class="cm-sidebar__nav-icon" />
                Resource Pools
            </NavLink>
        </li>
        <li class="cm-sidebar__nav-item">
            <NavLink class="nav-link" href="global-rate-limits">
                <RadzenIcon Icon="speed" class="cm-sidebar__nav-icon" />
                Global Rate Limits
            </NavLink>
        </li>
        <li class="cm-sidebar__nav-item">
            <NavLink class="nav-link" href="allocations">
                <RadzenIcon Icon="swap_horiz" class="cm-sidebar__nav-icon" />
                Active Allocations
            </NavLink>
        </li>
    </ul>

    <div class="cm-sidebar__footer">
        <div class="cm-sidebar__nav-item">
            <a href="#" class="nav-link">
                <RadzenIcon Icon="settings" class="cm-sidebar__nav-icon" />
                Settings
            </a>
        </div>
    </div>
</aside>
```

### 4. Rebuild `MainLayout.razor`

Replace the content of `ClientManager.AdminUI/Components/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase

<NavMenu />
<div class="cm-main">
    @Body
</div>
<RadzenComponents />
```

### 5. Add Radzen Material Icons reference

Ensure `App.razor` `<head>` includes the Material Icons font (used by `RadzenIcon`):

```html
<link rel="stylesheet" href="https://fonts.googleapis.com/icon?family=Material+Icons">
```

Note: Radzen's CSS may already include this. Verify during implementation — if icons render without this link, skip it.

## Verification

- App compiles and runs without errors

### Required: Browser Verification

Before marking this step complete, the implementer **must**:
1. Ensure the API project is running in a background terminal (start it if not already running).
2. Ensure the AdminUI project is running in a background terminal (restart it to pick up changes).
3. Open the AdminUI URL in the shared browser (using `open_browser_page`).
4. Take a screenshot and verify:
   - Sidebar renders on the left side with white background
   - "ClientManager" brand displays at top with a colored icon
   - All 6 nav items show with Material icons
   - Active nav item has indigo/purple filled pill background
   - Main content area has light gray background
   - Settings link appears at bottom of sidebar, separated by a border
   - Layout is not broken — content doesn't overlap sidebar
   - No console errors
5. Click at least 2 different nav items and screenshot to confirm active-state pill changes correctly.
6. Share screenshots with the user for sign-off before proceeding to the next step.
