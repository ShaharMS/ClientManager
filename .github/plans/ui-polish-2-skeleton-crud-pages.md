# Plan: UI Polish — Step 2: Skeleton Loaders on CRUD Pages

> **Status**: 🔲 Not started
> **Prerequisite**: [ui-polish-1-skeleton-foundation.md](ui-polish-1-skeleton-foundation.md)
> **Next**: [ui-polish-3-skeleton-dashboard-monitor.md](ui-polish-3-skeleton-dashboard-monitor.md)
> **Parent**: [ui-polish-overview.md](ui-polish-overview.md)

## TL;DR

Replace the plain `<p>Loading...</p>` text on all CRUD list and editor pages with the skeleton components created in Step 1. List pages get `<TableSkeleton />`, editor pages get `<FormSkeleton />`.

## Reference Pattern

All list pages follow the same structure — example in [ClientManager.AdminUI/Components/Pages/Clients/ClientList.razor](../../ClientManager.AdminUI/Components/Pages/Clients/ClientList.razor):
```razor
@if (_loading)
{
    <p>Loading...</p>
}
else if (_error is not null)
{
    ...
}
else
{
    <div class="cm-list-page__table-card">...</div>
}
```

All editor pages follow the same structure — example in [ClientManager.AdminUI/Components/Pages/Clients/ClientEditor.razor](../../ClientManager.AdminUI/Components/Pages/Clients/ClientEditor.razor):
```razor
@if (_loading)
{
    <p>Loading...</p>
}
else if (_error is not null)
{
    ...
}
else
{
    <div class="cm-editor">...</div>
}
```

## Steps

### 1. Replace loading indicator on list pages

For each of the following files, replace `<p>Loading...</p>` with `<TableSkeleton />`:

- `ClientManager.AdminUI/Components/Pages/Clients/ClientList.razor`
- `ClientManager.AdminUI/Components/Pages/Services/ServiceList.razor`
- `ClientManager.AdminUI/Components/Pages/ResourcePools/ResourcePoolList.razor`
- `ClientManager.AdminUI/Components/Pages/RateLimits/RateLimitList.razor`
- `ClientManager.AdminUI/Components/Pages/Quotas/QuotaList.razor`

Change:
```razor
@if (_loading)
{
    <p>Loading...</p>
}
```
To:
```razor
@if (_loading)
{
    <TableSkeleton />
}
```

### 2. Replace loading indicator on editor pages

For each of the following files, replace `<p>Loading...</p>` with `<FormSkeleton />`:

- `ClientManager.AdminUI/Components/Pages/Clients/ClientEditor.razor`
- `ClientManager.AdminUI/Components/Pages/Services/ServiceEditor.razor`
- `ClientManager.AdminUI/Components/Pages/ResourcePools/ResourcePoolEditor.razor`
- `ClientManager.AdminUI/Components/Pages/RateLimits/RateLimitEditor.razor`
- `ClientManager.AdminUI/Components/Pages/Quotas/QuotaEditor.razor`

Change:
```razor
@if (_loading)
{
    <p>Loading...</p>
}
```
To:
```razor
@if (_loading)
{
    <FormSkeleton />
}
```

## Verification

- Project compiles without errors
- **UI: Navigate to `/clients` — verify a table-shaped skeleton with smooth shimmer appears briefly before data loads**
- **UI: Navigate to `/clients/new` then `/clients/{some-id}` — verify a form-shaped skeleton appears briefly**
- **UI: Navigate to `/services`, `/resource-pools`, `/rate-limits`, `/quotas` — verify table skeletons appear on each**
- **UI: Navigate to `/services/{id}`, `/resource-pools/{id}`, `/rate-limits/{id}`, `/quotas/{id}` — verify form skeletons appear on each**
- **UI: Take a screenshot of at least one list and one editor page during the loading state to confirm the shimmer effect is visible**
