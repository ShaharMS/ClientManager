# Plan: ClientManager — Step 6: API Endpoints

> **Status**: ✅ Completed
> **Prerequisite**: [client-manager-5-access-control.md](client-manager-5-access-control.md)
> **Next**: [client-manager-7-logging.md](client-manager-7-logging.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Create all REST API controllers: A client-centric `ClientConfigurationsController` with sub-resource endpoints for managing per-service and per-pool settings, system-wide admin controllers for services, resource pools, and global rate limits, plus operational endpoints for checking access and acquiring/releasing resources. The previous separate `ClientsController`, `AccessRulesController`, and `RateLimitPoliciesController` are consolidated into a single `ClientConfigurationsController` that manages the whole client document with sub-resource routes.

Controllers do **not** handle error responses manually. Services throw typed exceptions (`NotFoundException`, `ConflictException`, `ClientDisabledException`, `AccessDeniedException`, `RateLimitedException`, `ValidationException`) which propagate to the `ErrorHandlingMiddleware` (step 9) and are mapped to appropriate HTTP status codes there. Controllers only handle the success path and return 200.

## Reference Pattern

The existing project uses the ASP.NET Core controllers pattern (already configured in [ClientManager.Api/Program.cs](ClientManager.Api/Program.cs) with `AddControllers()` and `MapControllers()`).

## Steps

### 1. Admin: Client Configurations controller

**File: `ClientManager.Api/Controllers/ClientConfigurationsController.cs`**

Route: `api/clients`

This is the primary admin controller. It manages full `ClientConfiguration` documents and provides sub-resource routes for nested service access, resource pool, and global rate limit settings.

#### Top-level client config CRUD

| Method | Route | Description |
|--------|-------|-------------|
| GET | `api/clients` | List all client configurations |
| GET | `api/clients/{id}` | Get a specific client configuration (full document) |
| POST | `api/clients` | Create a new client configuration |
| PUT | `api/clients/{id}` | Update a client configuration (full document replace) |
| DELETE | `api/clients/{id}` | Delete a client configuration |

```csharp
[ApiController]
[Route("api/clients")]
public class ClientConfigurationsController : ControllerBase
{
    private readonly IClientConfigurationRepository _repository;
    // constructor injection
}
```

- POST body example:
```json
{
  "id": "client-a",
  "name": "Client A",
  "isEnabled": true,
  "contributesToGlobalLimits": true,
  "exemptFromGlobalLimits": false,
  "globalRateLimit": {
    "strategy": "FixedWindow",
    "maxRequests": 1000,
    "windowSeconds": 60,
    "tokensPerRefill": null
  },
  "services": {
    "s3": {
      "isAllowed": true,
      "contributesToGlobalLimit": null,
      "exemptFromGlobalLimit": false,
      "rateLimit": {
        "strategy": "SlidingWindow",
        "maxRequests": 100,
        "windowSeconds": 60,
        "tokensPerRefill": null
      }
    },
    "user-api": {
      "isAllowed": true,
      "contributesToGlobalLimit": null,
      "exemptFromGlobalLimit": null,
      "rateLimit": null
    }
  },
  "resourcePools": {
    "s3-connections": {
      "maxSlots": 3
    }
  }
}
```
- Return 201 Created with Location header on POST
- Services throw `NotFoundException` for unknown IDs (middleware returns 404) and `ConflictException` for duplicate IDs (middleware returns 409). Controllers do not manually check for these.

#### Sub-resource: Per-service access settings

| Method | Route | Description |
|--------|-------|-------------|
| GET | `api/clients/{id}/services` | List all service access entries for a client |
| GET | `api/clients/{id}/services/{serviceId}` | Get service access settings for a specific service |
| PUT | `api/clients/{id}/services/{serviceId}` | Create or update service access settings |
| DELETE | `api/clients/{id}/services/{serviceId}` | Remove service access (revoke access) |

- PUT body example:
```json
{
  "isAllowed": true,
  "contributesToGlobalLimit": null,
  "exemptFromGlobalLimit": false,
  "rateLimit": {
    "strategy": "SlidingWindow",
    "maxRequests": 100,
    "windowSeconds": 60,
    "tokensPerRefill": null
  }
}
```
- GET on `api/clients/{id}/services` returns the `Services` dictionary from the client config
- PUT uses `IClientConfigurationRepository.SetServiceSettingsAsync`
- DELETE uses `IClientConfigurationRepository.RemoveServiceSettingsAsync`
- `NotFoundException` from repository propagates to middleware for unknown client IDs

#### Sub-resource: Per-pool resource settings

| Method | Route | Description |
|--------|-------|-------------|
| GET | `api/clients/{id}/resource-pools` | List all resource pool settings for a client |
| GET | `api/clients/{id}/resource-pools/{poolId}` | Get resource pool settings for a specific pool |
| PUT | `api/clients/{id}/resource-pools/{poolId}` | Create or update resource pool settings |
| DELETE | `api/clients/{id}/resource-pools/{poolId}` | Remove resource pool settings |

- PUT body example:
```json
{
  "maxSlots": 3
}
```
- Uses `IClientConfigurationRepository.SetResourcePoolSettingsAsync` / `RemoveResourcePoolSettingsAsync`
- `NotFoundException` from repository propagates to middleware for unknown client IDs

#### Sub-resource: Client global rate limit

| Method | Route | Description |
|--------|-------|-------------|
| GET | `api/clients/{id}/global-rate-limit` | Get the client's global rate limit |
| PUT | `api/clients/{id}/global-rate-limit` | Set the client's global rate limit |
| DELETE | `api/clients/{id}/global-rate-limit` | Remove the client's global rate limit |

- PUT body example:
```json
{
  "strategy": "TokenBucket",
  "maxRequests": 500,
  "windowSeconds": 60,
  "tokensPerRefill": 10
}
```
- GET returns `NotFoundException` (via middleware → 404) if no global rate limit is set
- PUT/DELETE load the full config, modify `GlobalRateLimit`, and save via `UpdateAsync`

### 2. Admin: Services controller

**File: `ClientManager.Api/Controllers/ServicesController.cs`**

Route: `api/services`

System-wide service definitions. Same CRUD shape as before.

| Method | Route | Description |
|--------|-------|-------------|
| GET | `api/services` | List all services |
| GET | `api/services/{id}` | Get a specific service |
| POST | `api/services` | Create a new service |
| PUT | `api/services/{id}` | Update a service |
| DELETE | `api/services/{id}` | Delete a service |

### 3. Admin: Resource Pools controller

**File: `ClientManager.Api/Controllers/ResourcePoolsController.cs`**

Route: `api/resource-pools`

System-wide resource pool definitions.

| Method | Route | Description |
|--------|-------|-------------|
| GET | `api/resource-pools` | List all resource pools |
| GET | `api/resource-pools/{id}` | Get a specific resource pool |
| POST | `api/resource-pools` | Create a resource pool |
| PUT | `api/resource-pools/{id}` | Update a resource pool |
| DELETE | `api/resource-pools/{id}` | Delete a resource pool |

- POST body example:
```json
{
  "id": "s3-connections",
  "name": "S3 Connection Pool",
  "maxSlots": 10,
  "allocationTtlSeconds": 300
}
```

### 4. Admin: Global Rate Limits controller

**File: `ClientManager.Api/Controllers/GlobalRateLimitsController.cs`**

Route: `api/global-rate-limits`

System-wide catch-all rate limits for services and resource pools.

| Method | Route | Description |
|--------|-------|-------------|
| GET | `api/global-rate-limits` | List all global rate limits |
| GET | `api/global-rate-limits?targetType={Service\|ResourcePool}` | Filter by target type |
| GET | `api/global-rate-limits/{id}` | Get a specific global rate limit |
| POST | `api/global-rate-limits` | Create a global rate limit |
| PUT | `api/global-rate-limits/{id}` | Update a global rate limit |
| DELETE | `api/global-rate-limits/{id}` | Delete a global rate limit |

- POST body example:
```json
{
  "id": "s3-global-limit",
  "targetId": "s3",
  "targetType": "Service",
  "strategy": "SlidingWindow",
  "maxRequests": 1000,
  "windowSeconds": 60,
  "tokensPerRefill": null
}
```
- Validate that the `targetId` references an existing service or resource pool depending on `targetType`
- `ConflictException` thrown (via middleware → 409) if a global limit already exists for the same `targetId` + `targetType` pair

### 5. Operational: Access Check controller

**File: `ClientManager.Api/Controllers/AccessCheckController.cs`**

Route: `api/access`

| Method | Route | Description |
|--------|-------|-------------|
| POST | `api/access/check` | Check if a client can access a service right now |
| GET | `api/access/{clientId}` | Get full accessibility report for a client |

**POST `api/access/check`:**
- Body: `{ "clientId": "client-a", "serviceId": "s3" }`
- Calls `IAccessControlService.CheckAccessAsync`
- On success: returns 200 with `AccessCheckResponse` (client ID, service ID, remaining requests)
- All denial paths are exceptions handled by middleware: `NotFoundException` → 404, `ClientDisabledException` → 403, `AccessDeniedException` → 403, `RateLimitedException` → 429 with `Retry-After` header
- The controller itself only contains the success path — call service, return `Ok(response)`

**GET `api/access/{clientId}`:**
- Calls `IAccessControlService.GetClientAccessibilityAsync`
- `NotFoundException` propagates to middleware (→ 404)
- Returns 200 with `ClientAccessibilityResponse`

### 6. Operational: Resource Allocation controller

**File: `ClientManager.Api/Controllers/ResourceAllocationController.cs`**

Route: `api/resources`

| Method | Route | Description |
|--------|-------|-------------|
| POST | `api/resources/acquire` | Acquire a resource slot |
| POST | `api/resources/release` | Release a resource slot |

**POST `api/resources/acquire`:**
- Body: `{ "clientId": "client-a", "resourcePoolId": "s3-connections" }`
- Calls `IResourceAllocationService.AcquireAsync`
- On success: returns 200 with `ResourceAcquireResponse` (allocation ID, expiry)
- All denial paths are exceptions handled by middleware: `NotFoundException` → 404, `ClientDisabledException` → 403, `RateLimitedException` → 429
- The controller itself only contains the success path — call service, return `Ok(response)`

**POST `api/resources/release`:**
- Body: `{ "allocationId": "guid-here" }`
- Calls `IResourceAllocationService.ReleaseAsync`
- `NotFoundException` propagates to middleware (→ 404)
- Returns 200 if released

### 7. Add JSON serialization configuration

Configure `System.Text.Json` options in the controllers to handle:
- `TimeSpan` serialization as seconds (integer) for API-facing fields (`windowSeconds`, `allocationTtlSeconds`)
- Enum serialization as strings (e.g. `"SlidingWindow"` not `1`)
- Camel case property naming

This can be done in `Program.cs`:
```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```

> **Note on TimeSpan handling**: The `ClientRateLimit.Window` and `ResourcePool.AllocationTtl` are `TimeSpan` properties on the entity. The API should accept/return these as integer seconds (`windowSeconds`, `allocationTtlSeconds`). Use either a custom JSON converter or separate DTO types for the API surface that map seconds ↔ TimeSpan. Choose the simpler approach — a JSON converter applied to `TimeSpan` properties.

## Verification

- `dotnet build` succeeds
- Controllers do not catch or manually handle any exceptions — all propagate to `ErrorHandlingMiddleware`
- Controllers only contain the success path: call service, return `Ok(response)`
- `api/clients/{id}` returns the full nested client configuration document
- Sub-resource endpoints (`api/clients/{id}/services/{serviceId}`, etc.) correctly read/write nested settings
- POST `api/access/check` returns 429 with `Retry-After` header when rate limited
- POST `api/resources/acquire` returns allocation ID and expiry time
- POST `api/resources/acquire` returns 429 when per-client slot cap or system-wide pool limit is reached
- GET `api/access/{clientId}` returns the full accessibility report
- Enum values serialize as strings in JSON responses
- OpenAPI/Swagger documents all endpoints correctly (via `AddOpenApi()`)
