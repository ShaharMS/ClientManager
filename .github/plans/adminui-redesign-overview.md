# Plan: AdminUI Redesign — Metric-Style Dashboard

## Status: � In progress

## Overview

The current AdminUI is a basic Bootstrap-styled Blazor app with plain tables, simple stat cards, and minimal visual design. This plan redesigns the entire admin panel to match a modern "Metric" dashboard aesthetic — clean white sidebar with icon navigation, prominent stat cards, interactive charts with filtering, and polished data tables.

**Design Reference**: [Dribbble Metric Dashboard Video](https://cdn.dribbble.com/userupload/42834013/file/original-333c4f78536a41262709503fae3c7342.mp4)

Key design elements from the reference:
- Clean white/light-gray sidebar with icon-based navigation and a colored active-state pill
- Welcome header bar with search and user avatar
- Top row of stat cards (rounded, first card filled with primary color, others outlined)
- Side-by-side charts: line chart (left, larger) + donut chart (right)
- Data table at the bottom with search and filter controls
- Soft shadows, generous spacing, rounded corners throughout
- Indigo/purple primary color palette

**Current state**: Rookie Bootstrap layout — dark sidebar, plain tables, no charts, no theme system.
**Desired end state**: Polished Radzen Blazor Community-based UI with Indigo/Purple theme, CSS-variable-based theming, interactive filtered charts, and a rich summary table across all pages.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [adminui-redesign-1-foundation.md](adminui-redesign-1-foundation.md) | ✅ Install Radzen, set up CSS variable theme system, define color palette |
| 2 | [adminui-redesign-2-layout-nav.md](adminui-redesign-2-layout-nav.md) | ✅ Redesign MainLayout and NavMenu to match Metric sidebar style |
| 3 | [adminui-redesign-3-dashboard.md](adminui-redesign-3-dashboard.md) | ✅ Rebuild Dashboard with 5 stat cards, filtered charts, and summary table |
| 4 | [adminui-redesign-4-api-endpoints.md](adminui-redesign-4-api-endpoints.md) | Add new Statistics API endpoints for chart/table data |
| 5 | [adminui-redesign-5-list-pages.md](adminui-redesign-5-list-pages.md) | Restyle all list pages (Clients, Services, Resource Pools, etc.) with Radzen DataGrid |
| 6 | [adminui-redesign-6-editor-pages.md](adminui-redesign-6-editor-pages.md) | Restyle all editor/form pages with Radzen form components |

## Key Decisions

- **Component library** — Radzen Blazor Community (free, open-source) over MudBlazor or pure CSS. Provides DataGrid, Charts, form components out of the box.
- **Chart library** — Radzen built-in charts (`RadzenChart` with `RadzenLineSeries` / `RadzenDonutSeries`) for simpler integration over Chart.js or ApexCharts.
- **Theme system** — CSS custom properties (variables) in a dedicated theme file, swappable at dev-config level. No runtime light/dark toggle yet, but the variable structure supports adding one later.
- **Primary color** — Indigo/Purple matching the Metric reference, all defined as CSS variables for easy swapping.
- **Stat cards** — 5 cards (not the reference's 4): Clients, Services, Resource Pools, Global Usage (req/time), Global Pool Acquisition %.
- **Dashboard charts** — Line chart (usage over time with rate limit cap bound) + Donut chart (per-client usage). Both are filterable by service/resource pool AND client. Filtering on service shows rate limit data; filtering on resource pool shows slot acquisition data.
- **Dashboard table** — Client summary: ClientID, Who Am I (reserved), Accessible Services count, Total Service Rate Limit Cap, Accessible Pools count, Total Accessible Slots (used/total).
- **New API endpoints required** — The dashboard charts and table need usage-over-time and per-client summary data the API doesn't currently serve.
- **Sidebar navigation** — Same items as current (Dashboard, Clients, Services, Resource Pools, Global Rate Limits, Allocations), restyled with icons and Metric aesthetic.
