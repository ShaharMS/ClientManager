# Plan: ClientManager — Step 8: Statistics Collection

> **Status**: 🔲 Not started
> **Prerequisite**: [client-manager-7-logging.md](client-manager-7-logging.md)
> **Next**: [client-manager-9-middlewares.md](client-manager-9-middlewares.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Define OpenTelemetry meters, counters, and histograms that track request counts, rate limit decisions, resource allocation activity, and request latency. Metrics are dimensioned by `clientId`, `serviceId`, `clientId+serviceId`, `endpoint`, and `statusCode`. This step creates the instrumentation layer — the Prometheus exporter endpoint is wired in step 10.

## Reference Pattern

OpenTelemetry .NET custom metrics follow the `System.Diagnostics.Metrics` API:

- Create a `Meter` per logical area
- Use `Counter<T>` for monotonically increasing values (request counts, error counts)
- Use `Histogram<T>` for distributions (latency)
- Tag each measurement with relevant dimensions

```csharp
using System.Diagnostics.Metrics;

var meter = new Meter("ClientManager.Requests");
var counter = meter.CreateCounter<long>("requests.total");
counter.Add(1, new TagList { { "clientId", clientId }, { "serviceId", serviceId } });
```

## Steps

### 1. Add OpenTelemetry NuGet packages

**File: `ClientManager.Api/ClientManager.Api.csproj`**

```xml
<PackageReference Include="OpenTelemetry.Api" Version="1.*" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.*" />
```

### 2. Create the metrics definitions class

**File: `ClientManager.Api/Services/Instrumentation/ClientManagerMetrics.cs`**

A singleton class that holds all `Meter` and instrument instances.

```csharp
using System.Diagnostics.Metrics;

namespace ClientManager.Api.Services.Instrumentation;

public class ClientManagerMetrics
{
    public static readonly string MeterName = "ClientManager";

    private readonly Meter _meter;

    // Request tracking
    public Counter<long> RequestsTotal { get; }
    public Counter<long> RequestErrors { get; }
    public Histogram<double> RequestDuration { get; }

    // Rate limiting
    public Counter<long> RateLimitAllowed { get; }
    public Counter<long> RateLimitDenied { get; }
    public Counter<long> GlobalRateLimitHits { get; }

    // Resource allocation
    public Counter<long> ResourceAcquired { get; }
    public Counter<long> ResourceReleased { get; }
    public Counter<long> ResourceDenied { get; }
    public Counter<long> ResourceExpired { get; }

    // Access control
    public Counter<long> AccessGranted { get; }
    public Counter<long> AccessDenied { get; }

    public ClientManagerMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        RequestsTotal = _meter.CreateCounter<long>(
            "clientmanager.requests.total",
            description: "Total HTTP requests received");

        RequestErrors = _meter.CreateCounter<long>(
            "clientmanager.requests.errors",
            description: "Total HTTP request errors");

        RequestDuration = _meter.CreateHistogram<double>(
            "clientmanager.requests.duration",
            unit: "ms",
            description: "HTTP request duration in milliseconds");

        RateLimitAllowed = _meter.CreateCounter<long>(
            "clientmanager.ratelimit.allowed",
            description: "Rate limit checks that passed");

        RateLimitDenied = _meter.CreateCounter<long>(
            "clientmanager.ratelimit.denied",
            description: "Rate limit checks that were denied");

        GlobalRateLimitHits = _meter.CreateCounter<long>(
            "clientmanager.ratelimit.global_hits",
            description: "Global rate limit denials");

        ResourceAcquired = _meter.CreateCounter<long>(
            "clientmanager.resources.acquired",
            description: "Resource slots successfully acquired");

        ResourceReleased = _meter.CreateCounter<long>(
            "clientmanager.resources.released",
            description: "Resource slots released");

        ResourceDenied = _meter.CreateCounter<long>(
            "clientmanager.resources.denied",
            description: "Resource acquisition attempts denied");

        ResourceExpired = _meter.CreateCounter<long>(
            "clientmanager.resources.expired",
            description: "Resource allocations expired by cleanup");

        AccessGranted = _meter.CreateCounter<long>(
            "clientmanager.access.granted",
            description: "Access checks that passed");

        AccessDenied = _meter.CreateCounter<long>(
            "clientmanager.access.denied",
            description: "Access checks that were denied");
    }
}
```

### 3. Instrument the rate limit service

**File: `ClientManager.Api/Services/RateLimiting/RateLimitService.cs`**

Inject `ClientManagerMetrics` into the service constructor. After each rate limit evaluation:

```csharp
if (result.IsAllowed)
    _metrics.RateLimitAllowed.Add(1, new TagList
    {
        { "clientId", clientId },
        { "serviceId", serviceId }
    });
else
    _metrics.RateLimitDenied.Add(1, new TagList
    {
        { "clientId", clientId },
        { "serviceId", serviceId }
    });
```

For global limit hits, also increment `GlobalRateLimitHits`.

### 4. Instrument the access control service

**File: `ClientManager.Api/Services/AccessControlService.cs`**

Inject `ClientManagerMetrics`. After determining the access result:

```csharp
if (response.IsAllowed)
    _metrics.AccessGranted.Add(1, new TagList
    {
        { "clientId", clientId },
        { "serviceId", serviceId }
    });
else
    _metrics.AccessDenied.Add(1, new TagList
    {
        { "clientId", clientId },
        { "serviceId", serviceId },
        { "reason", response.Reason }
    });
```

### 5. Instrument the resource allocation service

**File: `ClientManager.Api/Services/ResourceAllocationService.cs`**

Inject `ClientManagerMetrics`. Record metrics on acquire/release/deny/expire:

```csharp
// On successful acquire
_metrics.ResourceAcquired.Add(1, new TagList
{
    { "clientId", clientId },
    { "resourcePoolId", resourcePoolId }
});

// On denial (no slots / cap reached)
_metrics.ResourceDenied.Add(1, new TagList
{
    { "clientId", clientId },
    { "resourcePoolId", resourcePoolId },
    { "reason", "no_slots" }  // or "client_cap_reached" or "rate_limited"
});

// On release
_metrics.ResourceReleased.Add(1, new TagList
{
    { "allocationId", allocationId }
});

// In cleanup service — expired allocations
_metrics.ResourceExpired.Add(1, new TagList
{
    { "resourcePoolId", allocation.ResourcePoolId }
});
```

### 6. Register `ClientManagerMetrics` as singleton

**File: `ClientManager.Api/Program.cs`** (documented here, wired in step 11)

```csharp
builder.Services.AddSingleton<ClientManagerMetrics>();
```

## Verification

- `dotnet build` succeeds with OpenTelemetry packages
- `ClientManagerMetrics` can be resolved from DI
- All counters and histograms are created without errors
- Rate limit service increments `RateLimitAllowed` / `RateLimitDenied` on each evaluation
- Access control service increments `AccessGranted` / `AccessDenied` on each check
- Resource allocation service increments `ResourceAcquired` / `ResourceDenied` / `ResourceReleased` / `ResourceExpired` appropriately
- Metrics carry correct tag dimensions (`clientId`, `serviceId`, `resourcePoolId`)
