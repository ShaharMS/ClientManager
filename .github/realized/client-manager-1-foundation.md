# Plan: ClientManager — Step 1: Foundation

> **Status**: ✅ Completed
> **Prerequisite**: [client-manager-0-project-rename.md](client-manager-0-project-rename.md)
> **Next**: [client-manager-2-persistence.md](client-manager-2-persistence.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Define all domain records, enums, exception types, DTOs, and data access interfaces that the rest of the system depends on. This step produces zero runtime behavior — only type definitions and abstractions. Uses a **client-centric configuration model** where each client is a single document with nested service access settings, rate limits, and resource pool quotas. All data-only types use `record` (or `record struct` for small value types) instead of `class`. Typed exceptions for the throw-based error handling pattern are defined here. Entity models and enums live in `ClientManager.Shared`. Data access interfaces and implementations live in `ClientManager.DataAccess`. Request/response DTOs, exception types, and service interfaces remain in `ClientManager.Api`.

## Reference Pattern

This is a greenfield project. The existing codebase is a default ASP.NET Core 9.0 Web API template with a `WeatherForecast` controller. We will establish the folder structure and coding conventions from scratch.

In [ClientManager.Api/Program.cs](ClientManager.Api/Program.cs):
- Standard ASP.NET Core 9.0 minimal hosting model
- Controllers-based routing (already configured)

## Steps

### 1. Remove scaffolding

Delete the default template files that won't be used:
- `ClientManager.Api/WeatherForecast.cs`
- `ClientManager.Api/Controllers/WeatherForecastController.cs`

### 2. Create `ClientManager.Shared` class library project

Create a new class library project at `ClientManager.Shared/ClientManager.Shared.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

Add the project to `ClientManager.slnx`:
```xml
<Project Path="ClientManager.Shared/ClientManager.Shared.csproj" />
```

### 3. Create `ClientManager.DataAccess` class library project

Create a new class library project at `ClientManager.DataAccess/ClientManager.DataAccess.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ClientManager.Shared\ClientManager.Shared.csproj" />
  </ItemGroup>

</Project>
```

Add a project reference from `ClientManager.Api.csproj` to both `ClientManager.DataAccess` and `ClientManager.Shared`:
```xml
<ProjectReference Include="..\ClientManager.DataAccess\ClientManager.DataAccess.csproj" />
<ProjectReference Include="..\ClientManager.Shared\ClientManager.Shared.csproj" />
```

Add `ClientManager.DataAccess` to `ClientManager.slnx`:
```xml
<Project Path="ClientManager.DataAccess/ClientManager.DataAccess.csproj" />
```

### 3. Create folder structure

```
ClientManager.Shared/
  Models/
    Entities/
    Enums/

ClientManager.DataAccess/
  Interfaces/
  Implementations/           (empty for now — populated in Step 2)
    JsonFile/
    MongoDb/
    Redis/

ClientManager.Api/
  Models/
    Exceptions/
    Requests/
    Responses/
  Services/
  Interfaces/
  Controllers/
```

### 4. Define enums

**File: `ClientManager.Shared/Models/Enums/RateLimitStrategy.cs`**
```csharp
namespace ClientManager.Shared.Models.Enums;

public enum RateLimitStrategy
{
    FixedWindow,
    SlidingWindow,
    TokenBucket
}
```

**File: `ClientManager.Shared/Models/Enums/PersistenceProvider.cs`**
```csharp
namespace ClientManager.Shared.Models.Enums;

public enum PersistenceProvider
{
    JsonFile,
    MongoDb,
    Redis
}
```

**File: `ClientManager.Shared/Models/Enums/GlobalRateLimitTarget.cs`**
```csharp
namespace ClientManager.Shared.Models.Enums;

public enum GlobalRateLimitTarget
{
    Service,
    ResourcePool
}
```

### 5. Define the client-centric configuration model

The core of the data model. A single `ClientConfiguration` document per client, keyed by `Id`, containing all per-client settings as nested records. All data types use `record` (reference type) or `record struct` (small value types).

**File: `ClientManager.Shared/Models/Entities/ClientConfiguration.cs`**

The root document for a client. Contains top-level flags and nested dictionaries for per-service and per-pool settings.

```csharp
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

public record ClientConfiguration
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public bool ContributesToGlobalLimits { get; init; } = true;
    public bool ExemptFromGlobalLimits { get; init; } = false;
    public ClientRateLimit? GlobalRateLimit { get; init; }
    public Dictionary<string, ServiceAccessSettings> Services { get; init; } = new();
    public Dictionary<string, ResourcePoolSettings> ResourcePools { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
```

**File: `ClientManager.Shared/Models/Entities/ServiceAccessSettings.cs`**

Per-service settings nested inside a `ClientConfiguration`. The dictionary key in `ClientConfiguration.Services` is the service ID.

```csharp
namespace ClientManager.Shared.Models.Entities;

public record ServiceAccessSettings
{
    public bool IsAllowed { get; init; } = true;
    public bool? ContributesToGlobalLimit { get; init; }    // null = inherit from ClientConfiguration.ContributesToGlobalLimits
    public bool? ExemptFromGlobalLimit { get; init; }        // null = inherit from ClientConfiguration.ExemptFromGlobalLimits
    public ClientRateLimit? RateLimit { get; init; }         // Per-service rate limit for this client
}
```

**File: `ClientManager.Shared/Models/Entities/ClientRateLimit.cs`**

A rate limit configuration. Used both as `ClientConfiguration.GlobalRateLimit` (per-client across all services) and as `ServiceAccessSettings.RateLimit` (per-client-per-service).

```csharp
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

public record ClientRateLimit
{
    public RateLimitStrategy Strategy { get; init; }
    public int MaxRequests { get; init; }                   // Max requests in the window (or bucket capacity for token bucket)
    public TimeSpan Window { get; init; }                   // Window duration (or refill interval for token bucket)
    public int? TokensPerRefill { get; init; }              // Only used for TokenBucket strategy
}
```

**File: `ClientManager.Shared/Models/Entities/ResourcePoolSettings.cs`**

Per-resource-pool settings for a client. Single-field value type — uses `record struct`.

```csharp
namespace ClientManager.Shared.Models.Entities;

public readonly record struct ResourcePoolSettings(int MaxSlots);
```

### 6. Define system-wide entities

These entities define what exists in the system. They are NOT nested in client configs.

**File: `ClientManager.Shared/Models/Entities/Service.cs`**
```csharp
namespace ClientManager.Shared.Models.Entities;

public record Service
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
```

**File: `ClientManager.Shared/Models/Entities/ResourcePool.cs`**

Defines a named pool of finite resources. `MaxSlots` here is the system-wide maximum. Individual clients may have a lower cap via `ResourcePoolSettings.MaxSlots`.

```csharp
namespace ClientManager.Shared.Models.Entities;

public record ResourcePool
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int MaxSlots { get; init; }                     // System-wide maximum concurrent allocations
    public TimeSpan AllocationTtl { get; init; }           // Auto-expiry for unreleased allocations
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
```

**File: `ClientManager.Shared/Models/Entities/GlobalRateLimit.cs`**

Defines a catch-all rate limit for a service or resource pool across ALL contributing clients.

```csharp
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

public record GlobalRateLimit
{
    public string Id { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public GlobalRateLimitTarget TargetType { get; init; }
    public RateLimitStrategy Strategy { get; init; }
    public int MaxRequests { get; init; }
    public TimeSpan Window { get; init; }
    public int? TokensPerRefill { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
```

**File: `ClientManager.Shared/Models/Entities/ResourceAllocation.cs`**

Runtime state: a single active allocation of a resource slot by a client.

```csharp
namespace ClientManager.Shared.Models.Entities;

public record ResourceAllocation
{
    public string Id { get; init; } = string.Empty;
    public string ResourcePoolId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public DateTime AcquiredAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; init; }
    public bool IsReleased { get; init; } = false;
}
```

### 7. Define request/response DTOs

**File: `ClientManager.Api/Models/Requests/CheckAccessRequest.cs`**
```csharp
namespace ClientManager.Api.Models.Requests;

public record CheckAccessRequest(string ClientId, string ServiceId);
```

**File: `ClientManager.Api/Models/Requests/AcquireResourceRequest.cs`**
```csharp
namespace ClientManager.Api.Models.Requests;

public record AcquireResourceRequest(string ClientId, string ResourcePoolId);
```

**File: `ClientManager.Api/Models/Requests/ReleaseResourceRequest.cs`**
```csharp
namespace ClientManager.Api.Models.Requests;

public record ReleaseResourceRequest(string AllocationId);
```

**File: `ClientManager.Api/Models/Responses/AccessCheckResponse.cs`**

Success-only response. If access is denied or rate limited, a typed exception is thrown instead.

```csharp
namespace ClientManager.Api.Models.Responses;

public record AccessCheckResponse
{
    public required string ClientId { get; init; }
    public required string ServiceId { get; init; }
    public int? RemainingRequests { get; init; }
}
```

**File: `ClientManager.Api/Models/Responses/ResourceAcquireResponse.cs`**

Success-only response. If slots are unavailable or rate limited, a `RateLimitedException` is thrown instead.

```csharp
namespace ClientManager.Api.Models.Responses;

public record ResourceAcquireResponse
{
    public required string AllocationId { get; init; }
    public DateTime ExpiresAt { get; init; }
}
```

**File: `ClientManager.Api/Models/Responses/ClientAccessibilityResponse.cs`**

Returns a full report of which services a client can access and current status.

```csharp
namespace ClientManager.Api.Models.Responses;

public record ClientAccessibilityResponse
{
    public required string ClientId { get; init; }
    public List<ServiceAccessibility> Services { get; init; } = [];
}

public record ServiceAccessibility
{
    public required string ServiceId { get; init; }
    public bool HasAccess { get; init; }
    public bool IsCurrentlyRateLimited { get; init; }
    public int? RemainingRequests { get; init; }
}
```

### 8. Define exception types

Typed exceptions for the throw-based error handling pattern. Services throw these; `ErrorHandlingMiddleware` (Step 9) catches them and maps to HTTP status codes.

**File: `ClientManager.Api/Models/Exceptions/NotFoundException.cs`**
```csharp
namespace ClientManager.Api.Models.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
```

**File: `ClientManager.Api/Models/Exceptions/ConflictException.cs`**
```csharp
namespace ClientManager.Api.Models.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
```

**File: `ClientManager.Api/Models/Exceptions/ValidationException.cs`**
```csharp
namespace ClientManager.Api.Models.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
```

**File: `ClientManager.Api/Models/Exceptions/ClientDisabledException.cs`**
```csharp
namespace ClientManager.Api.Models.Exceptions;

public class ClientDisabledException : Exception
{
    public string ClientId { get; }

    public ClientDisabledException(string clientId)
        : base($"Client '{clientId}' is disabled")
    {
        ClientId = clientId;
    }
}
```

**File: `ClientManager.Api/Models/Exceptions/AccessDeniedException.cs`**

Thrown when a client does not have access to a service (deny-by-default). Maps to HTTP 403.

```csharp
namespace ClientManager.Api.Models.Exceptions;

public class AccessDeniedException : Exception
{
    public string ClientId { get; }
    public string ServiceId { get; }

    public AccessDeniedException(string clientId, string serviceId)
        : base($"Client '{clientId}' does not have access to service '{serviceId}'")
    {
        ClientId = clientId;
        ServiceId = serviceId;
    }
}
```

**File: `ClientManager.Api/Models/Exceptions/RateLimitedException.cs`**

Thrown when a client is rate limited (any scope) or when resource slots are unavailable. Maps to HTTP 429 with `Retry-After` header.

```csharp
namespace ClientManager.Api.Models.Exceptions;

public class RateLimitedException : Exception
{
    public int? RetryAfterSeconds { get; }

    public RateLimitedException(string message, int? retryAfterSeconds = null)
        : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
```

### 9. Define repository interfaces

**File: `ClientManager.DataAccess/Interfaces/IEntityRepository.cs`**

Generic CRUD repository for simple entities (Service, ResourcePool).

```csharp
namespace ClientManager.DataAccess.Interfaces;

public interface IEntityRepository<T> where T : class
{
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
```

**File: `ClientManager.DataAccess/Interfaces/IClientConfigurationRepository.cs`**

The primary repository for client-centric configuration. Provides full CRUD plus sub-document queries.

```csharp
using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Interfaces;

public interface IClientConfigurationRepository
{
    Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default);
    Task UpdateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default);
    Task DeleteAsync(string clientId, CancellationToken cancellationToken = default);

    // Sub-document helpers
    Task<ServiceAccessSettings?> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
    Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken = default);
    Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);

    Task<ResourcePoolSettings?> GetResourcePoolSettingsAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default);
    Task SetResourcePoolSettingsAsync(string clientId, string resourcePoolId, ResourcePoolSettings settings, CancellationToken cancellationToken = default);
    Task RemoveResourcePoolSettingsAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default);
}
```

**File: `ClientManager.DataAccess/Interfaces/IGlobalRateLimitRepository.cs`**

Queries for global rate limits by target. System-wide entity.

```csharp
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Interfaces;

public interface IGlobalRateLimitRepository : IEntityRepository<GlobalRateLimit>
{
    Task<GlobalRateLimit?> GetByTargetAsync(string targetId, GlobalRateLimitTarget targetType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlobalRateLimit>> GetByTargetTypeAsync(GlobalRateLimitTarget targetType, CancellationToken cancellationToken = default);
}
```

### 10. Define operational state interfaces

These are for runtime state (counters, allocations) — they live in `ClientManager.DataAccess/Interfaces/`.

**File: `ClientManager.DataAccess/Interfaces/IRateLimitStateStore.cs`**

Low-level atomic counter operations for rate limiting state.

```csharp
namespace ClientManager.DataAccess.Interfaces;

public interface IRateLimitStateStore
{
    Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string key, CancellationToken cancellationToken = default);
    Task ResetAsync(string key, CancellationToken cancellationToken = default);
}
```

**File: `ClientManager.DataAccess/Interfaces/IResourceAllocationRepository.cs`**

Atomic operations for resource allocation state.

```csharp
using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Interfaces;

public interface IResourceAllocationRepository
{
    Task<ResourceAllocation?> GetByIdAsync(string allocationId, CancellationToken cancellationToken = default);
    Task<int> GetActiveCountAsync(string resourcePoolId, CancellationToken cancellationToken = default);
    Task<int> GetActiveCountByClientAsync(string resourcePoolId, string clientId, CancellationToken cancellationToken = default);
    Task CreateAsync(ResourceAllocation allocation, CancellationToken cancellationToken = default);
    Task MarkReleasedAsync(string allocationId, CancellationToken cancellationToken = default);
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}
```

### 11. Define service interfaces

These interfaces are business logic contracts — they stay in the `ClientManager.Api` project.

**File: `ClientManager.Api/Interfaces/IRateLimitService.cs`**

```csharp
namespace ClientManager.Api.Interfaces;

public record RateLimitResult
{
    public bool IsAllowed { get; init; }
    public int RemainingRequests { get; init; }
    public int? RetryAfterSeconds { get; init; }
    public bool IsGlobalLimitHit { get; init; }
}

public interface IRateLimitService
{
    Task<RateLimitResult> CheckAndIncrementAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
    Task<RateLimitResult> CheckGlobalAndIncrementAsync(string clientId, CancellationToken cancellationToken = default);
    Task<RateLimitResult> CheckGlobalServiceLimitAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
    Task<RateLimitResult> CheckGlobalResourcePoolLimitAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default);
    Task<RateLimitResult> CheckWithoutIncrementAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
}
```

**File: `ClientManager.Api/Interfaces/IResourceAllocationService.cs`**

```csharp
using ClientManager.Api.Models.Responses;

namespace ClientManager.Api.Interfaces;

public interface IResourceAllocationService
{
    Task<ResourceAcquireResponse> AcquireAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default);
    Task<bool> ReleaseAsync(string allocationId, CancellationToken cancellationToken = default);
    Task CleanupExpiredAllocationsAsync(CancellationToken cancellationToken = default);
}
```

**File: `ClientManager.Api/Interfaces/IAccessControlService.cs`**

```csharp
using ClientManager.Api.Models.Responses;

namespace ClientManager.Api.Interfaces;

public interface IAccessControlService
{
    Task<AccessCheckResponse> CheckAccessAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
    Task<ClientAccessibilityResponse> GetClientAccessibilityAsync(string clientId, CancellationToken cancellationToken = default);
}
```

## Verification

- All three projects compile without errors (`dotnet build`)
- No runtime behavior exists yet — only type definitions and interfaces
- `ClientManager.Shared` contains `ClientConfiguration`, `ServiceAccessSettings`, `ClientRateLimit`, `ResourcePoolSettings` as `record`/`record struct` under `Models/Entities/`
- `ClientManager.Shared` contains system-wide entities (`Service`, `ResourcePool`, `GlobalRateLimit`, `ResourceAllocation`) as `record` under `Models/Entities/`
- `ClientManager.Shared` contains enums under `Models/Enums/`
- `ClientManager.DataAccess` contains `IClientConfigurationRepository`, `IEntityRepository<T>`, `IGlobalRateLimitRepository`, `IRateLimitStateStore`, `IResourceAllocationRepository` under `Interfaces/`
- `ClientManager.DataAccess` has a project reference to `ClientManager.Shared`
- `ClientManager.Api` contains exception types (`NotFoundException`, `ConflictException`, `ValidationException`, `ClientDisabledException`, `AccessDeniedException`, `RateLimitedException`) under `Models/Exceptions/`
- `ClientManager.Api` contains request/response records under `Models/Requests/` and `Models/Responses/`
- `ClientManager.Api` contains service interfaces under `Interfaces/`
- `RateLimitResult` is a `record` in `ClientManager.Api/Interfaces/IRateLimitService.cs`
- `IResourceAllocationRepository` includes `GetActiveCountByClientAsync` for per-client slot cap checks
- `ClientManager.Api` contains request/response DTOs under `ClientManager.Api.Models.Requests` / `ClientManager.Api.Models.Responses`
- `ClientManager.Api` has project references to `ClientManager.DataAccess` and `ClientManager.Shared`
- Solution file includes all three projects
- `WeatherForecast.cs` and `WeatherForecastController.cs` are deleted
