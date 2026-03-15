# Plan: ClientManager — Step 5: Access Control Service

> **Status**: 🔲 Not started
> **Prerequisite**: [client-manager-4-resource-allocation.md](client-manager-4-resource-allocation.md)
> **Next**: [client-manager-6-api-endpoints.md](client-manager-6-api-endpoints.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Implement `IAccessControlService` with deny-by-default access checks that combine client configuration lookup with rate limit evaluation at all three scopes: per-client-per-service, per-client-global, and **global per-service catch-all**. Access rules are now read from `IClientConfigurationRepository` — a client has access to a service only if it has a `ServiceAccessSettings` entry for that service with `IsAllowed: true` in its `ClientConfiguration`. On denial, typed exceptions are thrown (`AccessDeniedException` for not-whitelisted, `RateLimitedException` for rate-limited) so that `ErrorHandlingMiddleware` maps them to passthrough HTTP status codes (403, 429). On success, returns an `AccessCheckResponse` with remaining request info. Also provides a read-only accessibility report (`GetClientAccessibilityAsync`).

## Reference Pattern

No existing reference. The access control logic follows this decision chain:
1. Does the client configuration exist and is it enabled? → No → denied
2. Does the service exist and is it enabled? → No → denied
3. Does the client's configuration contain a `Services[serviceId]` entry with `IsAllowed: true`? → No → denied (deny-by-default)
4. Is the global catch-all limit for this service exceeded (and the client is not exempt)? → Yes → denied with 429 semantics
5. Is the client currently rate limited (per-service or per-client-global)? → Yes → denied with 429 semantics

## Steps

### 1. Implement `IAccessControlService`

**File: `ClientManager.Api/Services/AccessControlService.cs`**

```csharp
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Models.Responses;

namespace ClientManager.Api.Services;

public class AccessControlService : IAccessControlService
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IRateLimitService _rateLimitService;

    public AccessControlService(
        IClientConfigurationRepository clientConfigRepository,
        IEntityRepository<Service> serviceRepository,
        IRateLimitService rateLimitService)
    { ... }
}
```

**`CheckAccessAsync(clientId, serviceId)`:**

1. Load `ClientConfiguration` via `IClientConfigurationRepository.GetByIdAsync(clientId)`. If not found → throw `NotFoundException("Client '{clientId}' not found")`. If `IsEnabled == false` → throw `ClientDisabledException(clientId)`
2. Load service by ID via `IEntityRepository<Service>.GetByIdAsync(serviceId)`. If not found → throw `NotFoundException("Service '{serviceId}' not found")`. If `IsEnabled == false` → throw `NotFoundException("Service '{serviceId}' is disabled")`
3. Check the client's service access: `config.Services.TryGetValue(serviceId, out var serviceSettings)`. If not found or `serviceSettings.IsAllowed == false` → throw `AccessDeniedException(clientId, serviceId)`
4. Check global service rate limit via `IRateLimitService.CheckGlobalServiceLimitAsync(clientId, serviceId)`. If denied → throw `RateLimitedException("Global service rate limit exceeded", result.RetryAfterSeconds)`
5. Check per-client rate limit via `IRateLimitService.CheckAndIncrementAsync(clientId, serviceId)`. If denied → throw `RateLimitedException("Rate limit exceeded", result.RetryAfterSeconds)`
6. Log: `Logger.Info("Access granted | {@Properties}", new { ClientId = clientId, ServiceId = serviceId })`
7. Return `AccessCheckResponse { ClientId, ServiceId, RemainingRequests = result.RemainingRequests }`

> **Important**: Rate limit counters are only incremented if the access check passes. The global service limit is checked before per-client limits — if the service is globally saturated, no point checking per-client limits. The global counter is incremented (for contributing clients) even if the client is exempt, so that the aggregate count stays accurate.

**`GetClientAccessibilityAsync(clientId)`:**

1. Load the `ClientConfiguration`. If not found → throw `NotFoundException("Client '{clientId}' not found")`
2. Load all services via `IEntityRepository<Service>.GetAllAsync()`
3. For each service:
   - Check if `config.Services` contains an entry for this service with `IsAllowed: true`
   - **Do NOT** increment rate limit counters for the report — this is a read-only status check
   - Use `IRateLimitService.CheckWithoutIncrementAsync(clientId, serviceId)` to get remaining requests without side effects
4. Return the full `ClientAccessibilityResponse`

## Verification

- `dotnet build` succeeds
- `CheckAccessAsync` throws `NotFoundException` for unknown clients or services
- `CheckAccessAsync` throws `ClientDisabledException` for disabled clients
- `CheckAccessAsync` throws `AccessDeniedException` for clients without a `Services` entry for the requested service (deny-by-default)
- `CheckAccessAsync` throws `RateLimitedException` when rate limited
- `CheckAccessAsync` returns `AccessCheckResponse` with `RemainingRequests` when all checks pass
- `GetClientAccessibilityAsync` throws `NotFoundException` for unknown clients
- `GetClientAccessibilityAsync` returns a full report without modifying rate limit counters
- Rate limit counters are only incremented on successful access checks (not on denied-at-access-rule stage)
- Service access is read from `ClientConfiguration.Services[serviceId]` instead of a separate `AccessRule` entity
