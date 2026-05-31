# Plan: Address ClientManager.Api Review Notes — Step 4: Services and Controllers

> **Status**: ✅ Completed
> **Prerequisite**: [api-cr-remediation-3-internal-transport-structure.md](api-cr-remediation-3-internal-transport-structure.md)
> **Next**: [api-cr-remediation-5-openapi-and-documentation.md](api-cr-remediation-5-openapi-and-documentation.md)
> **Parent**: [api-cr-remediation-overview.md](api-cr-remediation-overview.md)

## TL;DR

Finish the public API layering pass. Controllers should validate inputs and delegate to API services, not talk to storage-facing clients directly, and their response style should be consistent and domain-named rather than generic `result` or `response` plumbing.

## Reference Pattern

In [../../ClientManager.Api/Controllers/AccessCheckController.cs](../../ClientManager.Api/Controllers/AccessCheckController.cs):
- Keep controllers focused on input binding, delegation, and HTTP response shaping.
- Inject public service interfaces rather than storage-facing transport abstractions.

In [../../ClientManager.Api/Services/Implementations/AccessControlService.cs](../../ClientManager.Api/Services/Implementations/AccessControlService.cs):
- Use small, single-goal services that adapt public requests onto internal transport calls.
- Keep service class responsibilities narrow and documented.

In [../../ClientManager.Api/Services/Implementations/ResourceAllocationService.cs](../../ClientManager.Api/Services/Implementations/ResourceAllocationService.cs):
- Use one service per public domain concern rather than one giant catch-all adapter.
- Preserve thin service implementations where the transport boundary is already doing the real work.

In [../../ClientManager.Api/Controllers/ServicesController.cs](../../ClientManager.Api/Controllers/ServicesController.cs):
- Use the current direct-internal-client controllers as the migration inventory for this step.
- Standardize naming and response shaping as part of the controller rewrite, not as a later cosmetic pass.

## Steps

### 1. Introduce public service interfaces for the remaining API domains

Create public API services for the controller domains that still inject storage-facing clients directly, including at least:

- client configurations and nested configuration resources
- services catalog
- resource pools catalog
- global rate limits catalog
- statistics read operations
- metrics read operations

Follow the existing one-service-one-goal pattern already used by `IAccessControlService` and `IResourceAllocationService`.

### 2. Migrate controllers to those services and keep validation at the boundary

Rewrite the direct-internal-client controllers so they:

- inject a public service interface
- perform only controller-level validation or route/body normalization
- delegate all business/transport decisions below the controller
- rely on the exception pipeline from Step 2 instead of controller-side null checks

This is also where the statistics controller should fully stop owning helper logic that belongs in binders or shared request helpers.

### 3. Standardize response variable naming and controller response style

As controllers are touched, apply the review guidance about naming and response style:

- name local variables after the domain object they hold, not `result` or `response`
- assign the service return value to a clearly named variable before returning it
- keep controller methods visually consistent in how they return `Ok(...)`, `CreatedAtAction(...)`, or `NoContent()`

This should be done during the service migration so it does not become a disconnected cleanup pass.

### 4. Expand failure metadata where the public route already exposes it

As part of the controller rewrite, make sure the action-level response annotations align with the service/exception behavior introduced earlier. Routes that can legitimately surface `404`, `409`, `429`, or `503` should document those outcomes where applicable rather than only the success path.

## Verification

- `dotnet build ClientManager.Api/ClientManager.Api.csproj`
- Start the API and verify the migrated controllers no longer inject any `Services.Internal...` transport interface directly.
- UI: Navigate to `/clients` and `/clients/{id}`; verify list, detail, and edit flows still work through the new service layer.
- UI: Navigate to `/services` and `/resource-pools`; verify list pages and edit pages still load and save correctly.
- UI: Navigate to `/rate-limits`; verify list and editor pages still work after the global-rate-limit service migration.
- UI: Navigate to `/monitor` and `/`; verify statistics and dashboard widgets still load through the new statistics/metrics services.
- UI: Navigate to `/allocations` and perform one acquire/release flow if available in the UI; verify adjacent runtime endpoints remain unaffected by the service-layer cleanup.

## Iteration Bootstrap Metadata

- **Recommended iteration slug**: `api-cr-remediation-services-controllers`
- **Evidence to preserve**: one grep or review note proving controllers no longer import internal transport interfaces; one screenshot or note per major page family touched by the migration.
- **UI pages to check**: `/clients`, `/clients/{id}`, `/services`, `/services/{id}`, `/resource-pools`, `/resource-pools/{id}`, `/rate-limits`, `/monitor`, `/`
- **Commit guidance**: split large controller migrations by domain if needed, but keep each controller and its new service implementation in the same commit so reviews stay executable.