# Plan: Structured NLog Logging Standardization — Step 4: Migrate All Call Sites

> **Status**: ✅ Completed
> **Prerequisite**: [logging-standardization-3-adminui-config.md](logging-standardization-3-adminui-config.md)
> **Next**: None — this is the final step.
> **Parent**: [logging-standardization-overview.md](logging-standardization-overview.md)

## TL;DR

Replace all 26 `ILogger<T>` logging calls across the API's middleware and services with `IAppLogger<T>` calls. Each call site changes from `_logger.LogXxx("message | Key={Key}", value)` to `_logger.Xxx("message", exception?, new { Key = value })`. Constructor injection changes from `ILogger<T>` to `IAppLogger<T>`.

## Reference Pattern

The new pattern (established in Step 1):

```csharp
// Before:
private readonly ILogger<MyService> _logger;
_logger.LogInformation("Access granted | ClientId={ClientId}, ServiceId={ServiceId}", clientId, serviceId);
_logger.LogError(ex, "Something failed");

// After:
private readonly IAppLogger<MyService> _logger;
_logger.Info("Access granted", new { ClientId = clientId, ServiceId = serviceId });
_logger.Error("Something failed", ex);
```

All messages become static strings. All dynamic data moves to the `extraData` anonymous object. Exceptions use the `Exception` overload. No `null` parameters needed.

## Steps

### 1. Migrate `ErrorHandlingMiddleware`

File: [ClientManager.Api/Middleware/ErrorHandlingMiddleware.cs](ClientManager.Api/Middleware/ErrorHandlingMiddleware.cs)

**Change constructor injection:**
```csharp
// Before:
private readonly ILogger<ErrorHandlingMiddleware> _logger;
public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)

// After:
private readonly IAppLogger<ErrorHandlingMiddleware> _logger;
public ErrorHandlingMiddleware(RequestDelegate next, IAppLogger<ErrorHandlingMiddleware> logger)
```

**Add using:**
```csharp
using ClientManager.Shared.Logging;
```

**Migrate 7 logging calls:**

| Line | Before | After |
|------|--------|-------|
| 28 | `_logger.LogWarning("Resource not found \| Path={Path}, Detail={Detail}", ...)` | `_logger.Warn("Resource not found", new { Path = context.Request.Path.Value, Detail = exception.Message })` |
| 34 | `_logger.LogWarning("Conflict \| Path={Path}, Detail={Detail}", ...)` | `_logger.Warn("Conflict", new { Path = context.Request.Path.Value, Detail = exception.Message })` |
| 40 | `_logger.LogWarning("Validation failed \| Path={Path}, Detail={Detail}", ...)` | `_logger.Warn("Validation failed", new { Path = context.Request.Path.Value, Detail = exception.Message })` |
| 46 | `_logger.LogWarning("Access denied \| Path={Path}, ClientId={ClientId}, ServiceId={ServiceId}", ...)` | `_logger.Warn("Access denied", new { Path = context.Request.Path.Value, ClientId = exception.ClientId, ServiceId = exception.ServiceId })` |
| 52 | `_logger.LogWarning("Client disabled \| Path={Path}, ClientId={ClientId}", ...)` | `_logger.Warn("Client disabled", new { Path = context.Request.Path.Value, ClientId = exception.ClientId })` |
| 58 | `_logger.LogWarning("Rate limited \| Path={Path}, Detail={Detail}, RetryAfterSeconds={RetryAfterSeconds}", ...)` | `_logger.Warn("Rate limited", new { Path = context.Request.Path.Value, Detail = exception.Message, RetryAfterSeconds = exception.RetryAfterSeconds })` |
| 70 | `_logger.LogError(exception, "Unhandled exception \| Path={Path}, Method={Method}", ...)` | `_logger.Error("Unhandled exception", exception, new { Path = context.Request.Path.Value, Method = context.Request.Method })` |

### 2. Migrate `RequestTrackingMiddleware`

File: [ClientManager.Api/Middleware/RequestTrackingMiddleware.cs](ClientManager.Api/Middleware/RequestTrackingMiddleware.cs)

**Change constructor injection:**
```csharp
// Before:
private readonly ILogger<RequestTrackingMiddleware> _logger;
public RequestTrackingMiddleware(RequestDelegate next, ILogger<RequestTrackingMiddleware> logger)

// After:
private readonly IAppLogger<RequestTrackingMiddleware> _logger;
public RequestTrackingMiddleware(RequestDelegate next, IAppLogger<RequestTrackingMiddleware> logger)
```

**Add using:**
```csharp
using ClientManager.Shared.Logging;
```

**Migrate 2 logging calls:**

| Line | Before | After |
|------|--------|-------|
| 26 | `_logger.LogDebug("Request started \| TraceId={TraceId}, Method={Method}, Path={Path}, QueryString={QueryString}", ...)` | `_logger.Debug("Request started", new { TraceId = activity?.TraceId.ToString(), Method = context.Request.Method, Path = context.Request.Path.Value, QueryString = context.Request.QueryString.Value })` |
| 61 | `_logger.LogInformation("Request completed \| TraceId={TraceId}, Method={Method}, Path={Path}, StatusCode={StatusCode}, DurationMs={DurationMs}", ...)` | `_logger.Info("Request completed", new { TraceId = activity?.TraceId.ToString(), Method = context.Request.Method, Path = context.Request.Path.Value, StatusCode = statusCode, DurationMs = stopwatch.Elapsed.TotalMilliseconds })` |

### 3. Migrate `RateLimitService`

File: [ClientManager.Api/Services/RateLimiting/RateLimitService.cs](ClientManager.Api/Services/RateLimiting/RateLimitService.cs)

**Change constructor injection:**
```csharp
// Before: ILogger<RateLimitService> logger
// After: IAppLogger<RateLimitService> logger
```

**Add using:**
```csharp
using ClientManager.Shared.Logging;
```

**Migrate 3 logging calls:**

| Line | Before | After |
|------|--------|-------|
| ~97 | `_logger.LogDebug("Rate limit evaluated \| ClientId={ClientId}, ServiceId={ServiceId}, Allowed={Allowed}, Remaining={Remaining}", ...)` | `_logger.Debug("Rate limit evaluated", new { ClientId = clientId, ServiceId = serviceId, Allowed = result.IsAllowed, Remaining = result.RemainingRequests })` |
| ~161 | `_logger.LogInformation("Global service limit checked \| ClientId={ClientId}, ServiceId={ServiceId}, ContributesToGlobal={ContributesToGlobal}, ExemptFromGlobal={ExemptFromGlobal}, Allowed={Allowed}", ...)` | `_logger.Info("Global service limit checked", new { ClientId = clientId, ServiceId = serviceId, ContributesToGlobal = contributesToGlobal, ExemptFromGlobal = exemptFromGlobal, Allowed = result.IsAllowed })` |
| ~212 | `_logger.LogInformation("Global resource pool limit checked \| ClientId={ClientId}, ResourcePoolId={ResourcePoolId}, ContributesToGlobal={ContributesToGlobal}, ExemptFromGlobal={ExemptFromGlobal}, Allowed={Allowed}", ...)` | `_logger.Info("Global resource pool limit checked", new { ClientId = clientId, ResourcePoolId = resourcePoolId, ContributesToGlobal = contributesToGlobal, ExemptFromGlobal = exemptFromGlobal, Allowed = result.IsAllowed })` |

### 4. Migrate `AccessControlService`

File: [ClientManager.Api/Services/AccessControlService.cs](ClientManager.Api/Services/AccessControlService.cs)

**Change constructor injection:**
```csharp
// Before: ILogger<AccessControlService> logger
// After: IAppLogger<AccessControlService> logger
```

**Add using:**
```csharp
using ClientManager.Shared.Logging;
```

**Migrate 1 logging call:**

| Line | Before | After |
|------|--------|-------|
| ~126 | `_logger.LogInformation("Access granted \| ClientId={ClientId}, ServiceId={ServiceId}", ...)` | `_logger.Info("Access granted", new { ClientId = clientId, ServiceId = serviceId })` |

### 5. Migrate `ResourceAllocationService`

File: [ClientManager.Api/Services/ResourceAllocationService.cs](ClientManager.Api/Services/ResourceAllocationService.cs)

**Change constructor injection:**
```csharp
// Before: ILogger<ResourceAllocationService> logger
// After: IAppLogger<ResourceAllocationService> logger
```

**Add using:**
```csharp
using ClientManager.Shared.Logging;
```

**Migrate 3 logging calls:**

| Line | Before | After |
|------|--------|-------|
| ~164 | `_logger.LogInformation("Resource acquired \| ClientId={ClientId}, ResourcePoolId={ResourcePoolId}, AllocationId={AllocationId}, ExpiresAt={ExpiresAt}", ...)` | `_logger.Info("Resource acquired", new { ClientId = clientId, ResourcePoolId = resourcePoolId, AllocationId = allocationId, ExpiresAt = expiresAt })` |
| ~205 | `_logger.LogInformation("Resource released \| AllocationId={AllocationId}", ...)` | `_logger.Info("Resource released", new { AllocationId = allocationId })` |
| ~220 | `_logger.LogInformation("Expired allocations cleaned up \| Count={Count}", ...)` | `_logger.Info("Expired allocations cleaned up", new { Count = cleanedUp })` |

### 6. Migrate `DataSeedService`

File: [ClientManager.Api/Services/DataSeedService.cs](ClientManager.Api/Services/DataSeedService.cs)

**Change constructor injection:**
```csharp
// Before: ILogger<DataSeedService> logger
// After: IAppLogger<DataSeedService> logger
```

**Add using:**
```csharp
using ClientManager.Shared.Logging;
```

**Migrate 5 logging calls:**

| Line | Before | After |
|------|--------|-------|
| 58 | `_logger.LogInformation("Seeded service \| ServiceId={ServiceId}", service.Id)` | `_logger.Info("Seeded service", new { ServiceId = service.Id })` |
| 66 | `_logger.LogInformation("Seeded resource pool \| ResourcePoolId={ResourcePoolId}", pool.Id)` | `_logger.Info("Seeded resource pool", new { ResourcePoolId = pool.Id })` |
| 74 | `_logger.LogInformation("Seeded global rate limit \| GlobalRateLimitId={GlobalRateLimitId}", globalLimit.Id)` | `_logger.Info("Seeded global rate limit", new { GlobalRateLimitId = globalLimit.Id })` |
| 82 | `_logger.LogInformation("Seeded client configuration \| ClientId={ClientId}", config.Id)` | `_logger.Info("Seeded client configuration", new { ClientId = config.Id })` |
| 85 | `_logger.LogInformation("Data seeding completed")` | `_logger.Info("Data seeding completed")` |

### 7. Migrate `AllocationCleanupService`

File: [ClientManager.Api/Services/AllocationCleanupService.cs](ClientManager.Api/Services/AllocationCleanupService.cs)

**Change constructor injection:**
```csharp
// Before: ILogger<AllocationCleanupService> logger
// After: IAppLogger<AllocationCleanupService> logger
```

**Add using:**
```csharp
using ClientManager.Shared.Logging;
```

**Migrate 1 logging call:**

| Line | Before | After |
|------|--------|-------|
| 47 | `_logger.LogError(ex, "Error cleaning up expired resource allocations")` | `_logger.Error("Error cleaning up expired resource allocations", ex)` |

### 8. Migrate `UsagePersistenceService`

File: [ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs](ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs)

**Change constructor injection:**
```csharp
// Before: ILogger<UsagePersistenceService> logger
// After: IAppLogger<UsagePersistenceService> logger
```

**Add using:**
```csharp
using ClientManager.Shared.Logging;
```

**Migrate 3 logging calls:**

| Line | Before | After |
|------|--------|-------|
| ~79 | `_logger.LogError(ex, "Error in fast usage persistence cycle")` | `_logger.Error("Error in fast usage persistence cycle", ex)` |
| ~89 | `_logger.LogError(ex, "Error in slow usage persistence cycle")` | `_logger.Error("Error in slow usage persistence cycle", ex)` |
| ~180 | `_logger.LogDebug("Flushed {Count} usage counter groups to storage", grouped.Count)` | `_logger.Debug("Flushed usage counter groups to storage", new { Count = grouped.Count })` |

### 9. Remove unused `using Microsoft.Extensions.Logging` statements

After migrating, each file that no longer directly references `ILogger<T>` should have the `using Microsoft.Extensions.Logging;` removed if it has no other references to that namespace. Do NOT remove it from files that still use `LogLevel` or other types from that namespace. Check each file individually.

## Verification

- Full solution compiles without errors: `dotnet build ClientManager.slnx`
- No remaining references to `_logger.LogInformation`, `_logger.LogWarning`, `_logger.LogError`, `_logger.LogDebug`, `_logger.LogTrace`, or `_logger.LogCritical` in any `.cs` file (except `Program.cs` bootstrap which uses NLog directly).
- No string interpolation (`$"..."`) in any log message parameter.
- Run the API and confirm structured logs appear in console with `ExtraData.*` fields.
- Check `logs/clientmanager-*.log` JSON entries contain `ExtraData.*` properties.
- **UI: Start both API and AdminUI. Navigate to the Clients page — confirm data loads.**
- **UI: Navigate to the Active Allocations page — confirm it renders correctly.**
- **UI: Trigger a rate-limited request (if possible via the traffic generator) and verify the log output shows `ExtraData.ClientId`, `ExtraData.ServiceId`, etc.**
- **UI: Take a screenshot to confirm no layout breakage or error banners.**
