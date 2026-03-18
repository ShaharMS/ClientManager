# Plan: ClientManager — Step 4: Resource Allocation Engine

> **Status**: ✅ Completed
> **Prerequisite**: [client-manager-3-rate-limiting.md](client-manager-3-rate-limiting.md)
> **Next**: [client-manager-5-access-control.md](client-manager-5-access-control.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Implement the `IResourceAllocationService` that manages named resource pools. Clients acquire slots (bounded by both the system-wide `ResourcePool.MaxSlots` and the per-client `ResourcePoolSettings.MaxSlots` from `ClientConfiguration`), use the resource, then release the slot. Unreleased allocations auto-expire via a configurable TTL. A background `IHostedService` periodically cleans up expired allocations. Acquisition also checks the global resource pool rate limit (if configured) before granting a slot.

## Reference Pattern

No existing reference. This follows a distributed semaphore pattern:
- A pool has N max slots (system-wide) and each client may have a lower cap (per-client)
- Acquire checks both limits, if both allow then creates an allocation and returns an ID
- Release marks the allocation as released
- A background sweep cleans up expired allocations

## Steps

### 1. Implement `IResourceAllocationService`

**File: `ClientManager.Api/Services/ResourceAllocationService.cs`**

```csharp
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Models.Responses;

namespace ClientManager.Api.Services;

public class ResourceAllocationService : IResourceAllocationService
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationRepository _allocationRepository;
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IRateLimitService _rateLimitService;

    public ResourceAllocationService(
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationRepository allocationRepository,
        IClientConfigurationRepository clientConfigRepository,
        IRateLimitService rateLimitService)
    { ... }
}
```

**`AcquireAsync(clientId, resourcePoolId)`:**
1. Load the `ResourcePool` by ID. If not found → throw `NotFoundException("Resource pool '{resourcePoolId}' not found")`
2. Load the `ClientConfiguration` by `clientId`. If not found → throw `NotFoundException("Client '{clientId}' not found")`. If disabled → throw `ClientDisabledException(clientId)`
3. **Check per-client pool quota**: Look up `config.ResourcePools[resourcePoolId]`. If the entry exists, get the client's `MaxSlots` cap. Then call `IResourceAllocationRepository.GetActiveCountByClientAsync(resourcePoolId, clientId)`. If `clientActiveCount >= clientCap` → throw `RateLimitedException("Client slot limit reached for pool '{resourcePoolId}'")`
4. **Check the global resource pool rate limit** via `IRateLimitService.CheckGlobalResourcePoolLimitAsync(clientId, resourcePoolId)`. If denied → throw `RateLimitedException("Global resource pool rate limit exceeded", result.RetryAfterSeconds)`
5. **Check system-wide pool capacity**: Get the total active allocation count via `IResourceAllocationRepository.GetActiveCountAsync(resourcePoolId)`. If `activeCount >= pool.MaxSlots` → throw `RateLimitedException("No slots available in pool '{resourcePoolId}'")`
6. Create a new `ResourceAllocation`:
   - `Id` = new GUID string
   - `ResourcePoolId` = resourcePoolId
   - `ClientId` = clientId
   - `AcquiredAt` = `DateTime.UtcNow`
   - `ExpiresAt` = `DateTime.UtcNow + pool.AllocationTtl`
   - `IsReleased` = false
7. Save via `IResourceAllocationRepository.CreateAsync`
8. Log: `Logger.Info("Resource acquired | {@Properties}", new { ClientId = clientId, ResourcePoolId = resourcePoolId, AllocationId = id, ExpiresAt = expiresAt })`
9. Return `ResourceAcquireResponse { AllocationId = id, ExpiresAt = expiresAt }`

> **Note**: If the client has no entry in `config.ResourcePools` for this pool, skip the per-client cap check (no cap configured = no per-client limit, only the system-wide limit applies).

> **Concurrency note**: The check-then-insert is not atomic. For the Redis provider, this should use a Lua script or Redis transaction in the `IResourceAllocationRepository` implementation. For MongoDB, use a transaction or optimistic concurrency. For JSON file (dev only), the `SemaphoreSlim` in the repository is sufficient.

**`ReleaseAsync(allocationId)`:**
1. Load the allocation by ID. If not found → throw `NotFoundException("Allocation '{allocationId}' not found")`. If already released → return `false`
2. Mark as released via `IResourceAllocationRepository.MarkReleasedAsync`
3. Log: `Logger.Info("Resource released | {@Properties}", new { AllocationId = allocationId })`
4. Return `true`

**`CleanupExpiredAllocationsAsync()`:**
1. Call `IResourceAllocationRepository.CleanupExpiredAsync()`
2. Log the number of cleaned up allocations

### 2. Create the TTL cleanup hosted service

**File: `ClientManager.Api/Services/AllocationCleanupService.cs`**

A `BackgroundService` that periodically calls `IResourceAllocationService.CleanupExpiredAllocationsAsync`.

```csharp
using ClientManager.Api.Interfaces;

namespace ClientManager.Api.Services;

public class AllocationCleanupService : BackgroundService
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public AllocationCleanupService(
        IServiceScopeFactory scopeFactory)
    { ... }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IResourceAllocationService>();
                await service.CleanupExpiredAllocationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error cleaning up expired resource allocations");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
```

> **Key detail**: Uses `IServiceScopeFactory` to create a new scope each iteration, since `BackgroundService` is a singleton but the repositories may be scoped.

## Verification

- `dotnet build` succeeds
- `AcquireAsync` returns a `ResourceAcquireResponse` with allocation ID when both system-wide and per-client slots are available
- `AcquireAsync` throws `RateLimitedException` when system-wide pool is full
- `AcquireAsync` throws `RateLimitedException` when the client's per-pool slot cap is reached
- `AcquireAsync` throws `NotFoundException` when pool or client does not exist
- `AcquireAsync` throws `ClientDisabledException` when client is disabled
- `AcquireAsync` skips the per-client cap check when the client has no `ResourcePools` entry for that pool
- `ReleaseAsync` throws `NotFoundException` for unknown allocations, returns `true` for valid and `false` for already-released
- `CleanupExpiredAllocationsAsync` removes allocations past their TTL
- `AllocationCleanupService` runs continuously and logs cleanup activity
