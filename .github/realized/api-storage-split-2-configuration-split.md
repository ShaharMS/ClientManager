# Plan: Split Public API from Storage Service — Step 2: Configuration Split

> **Status**: ✅ Completed
> **Prerequisite**: [api-storage-split-1-foundation.md](api-storage-split-1-foundation.md)
> **Next**: [api-storage-split-3-runtime-state.md](api-storage-split-3-runtime-state.md)
> **Parent**: [api-storage-split-overview.md](api-storage-split-overview.md)

## TL;DR

Move configuration CRUD and catalog-style queries into `ClientManager.StorageApi` first, then make the public API proxy those calls through typed clients. This is the lowest-risk slice of the split and creates a stable internal contract for the Admin UI’s read-heavy pages before the hot path is migrated, while reusing the current shared entities and search/request/response models instead of inventing duplicate configuration DTOs.

## Reference Pattern

In [../../ClientManager.Api/Controllers/ServicesController.cs](../../ClientManager.Api/Controllers/ServicesController.cs):
- Use the existing route shapes, response codes, and Swagger metadata as the public contract that must remain stable.
- Replace direct repository access with service delegation; do not keep storage interfaces in the controller constructor.

In [../../ClientManager.Api/Controllers/AccessCheckController.cs](../../ClientManager.Api/Controllers/AccessCheckController.cs):
- Follow the thin-controller pattern where controllers validate inputs and delegate business behavior to a service abstraction.
- Keep action methods simple so transport concerns stay outside the controller body.

In [../../ClientManager.AdminUI/Services/ClientApiService.cs](../../ClientManager.AdminUI/Services/ClientApiService.cs):
- Mirror the small, focused HTTP wrapper approach for the new internal clients.
- Invalidate any local cache on write methods instead of letting stale catalog data linger.

## Steps

### 1. Add storage-side configuration services and controllers

In `ClientManager.StorageApi`, introduce service abstractions for each configuration concern that currently hits repositories or database interfaces directly from the public API controllers: clients, services, resource pools, and global rate limits.

Move the existing CRUD and search behavior behind those storage-side services, then expose internal controller endpoints that keep the same request and response shapes already used by the public API. Reuse the existing shared entity and query types for these endpoints. If an internal batch contract is truly needed, define it once in `ClientManager.Shared` and remove any host-local duplicate.

```csharp
public interface IServiceCatalogService
{
    Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);
    Task<Service?> GetByIdAsync(string id, CancellationToken cancellationToken);
}
```

### 2. Add typed configuration clients in the public API

Implement `HttpClient`-backed clients in `ClientManager.Api` for the configuration concerns moved above. Group them by business capability instead of by raw endpoint string constants.

The client layer should translate HTTP into method calls, keep public API controllers unaware of internal URLs, and own any temporary cache invalidation needed for read-mostly catalog calls. Keep these clients thin and avoid rebuilding configuration business rules in `ClientManager.Api`.

### 3. Refactor public configuration controllers to proxy through the new clients

Update `ClientConfigurationsController`, `ServicesController`, `ResourcePoolsController`, and `GlobalRateLimitsController` so they depend on façade services or internal clients instead of `ClientManager.DataAccess` abstractions.

Preserve the public routes, request bodies, response payloads, and response codes exactly so Admin UI and scripts do not need to change during the split. As each controller is migrated, delete the old direct repository or database dependency instead of leaving both paths in place.

## Verification

- Public API configuration endpoints still return the same payload shapes and status codes after the proxy refactor.
- Configuration endpoints reuse shared models from `ClientManager.Shared`; no new `ClientManager.Api` or `ClientManager.StorageApi` copies of service, pool, client, rate-limit, or query contracts remain.
- `ClientManager.StorageApi` can serve the configuration CRUD and search endpoints directly when exercised through Swagger.
- UI: Navigate to `/clients`, open an existing client, and verify the editor loads all sections without missing data.
- UI: Navigate to `/services` and `/resource-pools`, open an existing item from each list, and verify the editor pages load correctly.
- UI: Create or edit a rate limit from `/rate-limits` and verify save completes without an error toast and the list refreshes with the change.
