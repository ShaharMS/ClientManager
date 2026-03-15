# Plan: ClientManager — Step 10: Statistics Endpoints

> **Status**: 🔲 Not started
> **Prerequisite**: [client-manager-9-middlewares.md](client-manager-9-middlewares.md)
> **Next**: [client-manager-11-startup-config.md](client-manager-11-startup-config.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Create a `StatisticsController` with JSON endpoints that return human-readable statistics (request counts, rate limit stats, resource pool usage), and wire the OpenTelemetry Prometheus exporter to expose a `/metrics` endpoint for Prometheus scraping. The JSON endpoints query the persistence layer for current state; the Prometheus endpoint is served automatically by OpenTelemetry from the meters defined in step 8.

## Reference Pattern

The controllers follow the same pattern as other controllers in the project (step 6). The Prometheus endpoint uses `OpenTelemetry.Exporter.Prometheus.AspNetCore` which adds a single `app.MapPrometheusScrapingEndpoint()` call.

## Steps

### 1. Create `StatisticsController`

**File: `ClientManager.Api/Controllers/StatisticsController.cs`**

Route: `api/statistics`

This controller provides human-readable JSON statistics. It queries repositories directly for current counts.

```csharp
using ClientManager.DataAccess.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

[ApiController]
[Route("api/statistics")]
public class StatisticsController : ControllerBase
{
    private readonly IResourceAllocationRepository _allocationRepository;
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _poolRepository;

    // constructor injection
}
```

#### Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `api/statistics/overview` | High-level system overview |
| GET | `api/statistics/clients` | Per-client request/access stats |
| GET | `api/statistics/clients/{clientId}` | Detailed stats for one client |
| GET | `api/statistics/services` | Per-service usage stats |
| GET | `api/statistics/resource-pools` | Per-pool allocation stats |

### 2. Implement the overview endpoint

**GET `api/statistics/overview`:**

Returns a summary of current system state:

```json
{
  "totalClients": 12,
  "enabledClients": 10,
  "totalServices": 5,
  "enabledServices": 4,
  "totalResourcePools": 3,
  "activeAllocations": 27
}
```

Implementation:
1. Count all clients via `IClientConfigurationRepository.GetAllAsync()`
2. Count enabled clients (filter `IsEnabled`)
3. Count services and enabled services
4. Count resource pools
5. Count active allocations via `IResourceAllocationRepository.GetActiveCountAsync()` across all pools

### 3. Implement per-client statistics

**GET `api/statistics/clients`:**

Returns a list of clients with their current state:

```json
[
  {
    "clientId": "client-a",
    "name": "Client A",
    "isEnabled": true,
    "serviceCount": 3,
    "resourcePoolCount": 1,
    "hasGlobalRateLimit": true
  }
]
```

**GET `api/statistics/clients/{clientId}`:**

Returns detailed stats for one client. Throws `NotFoundException` if client not found (middleware → 404).

```json
{
  "clientId": "client-a",
  "name": "Client A",
  "isEnabled": true,
  "services": {
    "s3": { "isAllowed": true, "hasRateLimit": true },
    "user-api": { "isAllowed": true, "hasRateLimit": false }
  },
  "resourcePools": {
    "s3-connections": {
      "maxSlots": 3,
      "activeAllocations": 2
    }
  },
  "globalRateLimit": {
    "strategy": "FixedWindow",
    "maxRequests": 1000,
    "windowSeconds": 60
  }
}
```

Implementation:
1. Load `ClientConfiguration` by ID
2. For each resource pool entry, get the active allocation count via `IResourceAllocationRepository.GetActiveCountByClientAsync(poolId, clientId)`

### 4. Implement per-service statistics

**GET `api/statistics/services`:**

```json
[
  {
    "serviceId": "s3",
    "name": "S3 Storage",
    "isEnabled": true,
    "clientCount": 8,
    "hasGlobalRateLimit": true
  }
]
```

Implementation:
1. Load all services
2. For each service, count how many client configurations have an entry for it in `Services`
3. Check if a `GlobalRateLimit` exists for this service

### 5. Implement resource pool statistics

**GET `api/statistics/resource-pools`:**

```json
[
  {
    "resourcePoolId": "s3-connections",
    "name": "S3 Connection Pool",
    "maxSlots": 10,
    "activeAllocations": 7,
    "availableSlots": 3,
    "hasGlobalRateLimit": false
  }
]
```

Implementation:
1. Load all resource pools
2. For each pool, get active allocation count via `IResourceAllocationRepository.GetActiveCountAsync(poolId)`
3. Calculate available slots

### 6. Define response records for statistics

**File: `ClientManager.Api/Models/Responses/StatisticsResponses.cs`**

```csharp
namespace ClientManager.Api.Models.Responses;

public record SystemOverviewResponse(
    int TotalClients,
    int EnabledClients,
    int TotalServices,
    int EnabledServices,
    int TotalResourcePools,
    int ActiveAllocations);

public record ClientSummaryResponse(
    string ClientId,
    string Name,
    bool IsEnabled,
    int ServiceCount,
    int ResourcePoolCount,
    bool HasGlobalRateLimit);

public record ServiceStatisticsResponse(
    string ServiceId,
    string Name,
    bool IsEnabled,
    int ClientCount,
    bool HasGlobalRateLimit);

public record ResourcePoolStatisticsResponse(
    string ResourcePoolId,
    string Name,
    int MaxSlots,
    int ActiveAllocations,
    int AvailableSlots,
    bool HasGlobalRateLimit);
```

### 7. Wire the Prometheus scraping endpoint

**File: `ClientManager.Api/Program.cs`** (documented here, wired in step 11)

Configure OpenTelemetry metrics with the Prometheus exporter:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddMeter(ClientManagerMetrics.MeterName);
        metrics.AddPrometheusExporter();
    });

// After app.Build():
app.MapPrometheusScrapingEndpoint("/metrics");
```

This exposes all `ClientManagerMetrics` counters and histograms in Prometheus text format at `/metrics`.

## Verification

- `dotnet build` succeeds
- GET `api/statistics/overview` returns correct counts for clients, services, pools, allocations
- GET `api/statistics/clients` returns per-client summaries
- GET `api/statistics/clients/{clientId}` returns detailed stats with active allocation counts
- GET `api/statistics/services` returns per-service client counts
- GET `api/statistics/resource-pools` returns pool utilization stats
- GET `/metrics` returns Prometheus-format metrics including `clientmanager_requests_total`, `clientmanager_ratelimit_allowed`, etc.
- Statistics response types are records
