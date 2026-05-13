# Plan: Hot Path Performance Observability — Step 2: Tracing Logs

> **Status**: ✅ Completed
> **Prerequisite**: [hot-path-performance-observability-1-baseline-runtime.md](hot-path-performance-observability-1-baseline-runtime.md)
> **Next**: [hot-path-performance-observability-3-storage-counters.md](hot-path-performance-observability-3-storage-counters.md)
> **Parent**: [hot-path-performance-observability-overview.md](hot-path-performance-observability-overview.md)

## TL;DR

Add distributed traces and structured timing logs so a single access check or allocation can be visualized from public request through StorageApi, rate-limit logic, document-store wrapper, actual storage operation, and response. This step should not change hot-path behavior; it makes the bottlenecks observable.

## Reference Pattern

Follow the existing metrics and logging shape.

In [ClientManagerMetrics.cs](ClientManager.Api/Utils/Instrumentation/ClientManagerMetrics.cs):
- Metrics are held in a singleton injected into middleware and services.
- Instrument names use the `clientmanager.*` prefix and milliseconds for duration histograms.

In [StorageApiMetrics.cs](ClientManager.StorageApi/Utils/Instrumentation/StorageApiMetrics.cs):
- StorageApi already has its own meter name and counters for access/resource decisions.
- Add operation-specific histograms here rather than creating ad hoc meters.

In [RequestTrackingMiddleware.cs](ClientManager.StorageApi/Middlewares/RequestTrackingMiddleware.cs):
- Request duration is already captured with a stopwatch and tagged by method/endpoint.
- Correlation IDs already flow into NLog scope.

In [nlog.config](ClientManager.StorageApi/nlog.config):
- JSON logs already include traceId, spanId, and correlationId fields.
- Trace/span fields will become useful once real ActivitySource spans exist.

## Steps

### 1. Add ActivitySource and operation histograms

Update [ClientManagerMetrics.cs](ClientManager.Api/Utils/Instrumentation/ClientManagerMetrics.cs) and [StorageApiMetrics.cs](ClientManager.StorageApi/Utils/Instrumentation/StorageApiMetrics.cs) with ActivitySource names and hot-path histograms. Keep all names stable and documented in the summary comments.

```csharp
public static readonly string ActivitySourceName = "ClientManager.StorageApi";
public ActivitySource ActivitySource { get; }
public Histogram<double> AccessCheckDuration { get; }
```

Add separate histograms for access checks, resource acquires, resource releases, storage client calls, and document-store operations.

### 2. Enable tracing in both hosts

Update [Program.cs](ClientManager.Api/Program.cs) and [Program.cs](ClientManager.StorageApi/Program.cs) to add OpenTelemetry tracing alongside existing metrics. Add required packages to [ClientManager.Api.csproj](ClientManager.Api/ClientManager.Api.csproj) and [ClientManager.StorageApi.csproj](ClientManager.StorageApi/ClientManager.StorageApi.csproj), including OTLP export and HttpClient instrumentation.

Configure tracing so it works without a collector and exports when `Observability:OtlpEndpoint` is configured. Include AspNetCore, HttpClient, and both ActivitySource names.

### 3. Trace public API to StorageApi calls

Instrument [RuntimeStateClient.cs](ClientManager.Api/Services/InternalClients/Implementations/RuntimeStateClient.cs) around access, acquire, and release calls. HttpClient instrumentation should capture the outbound request; custom spans should add domain tags such as client ID, service ID, resource pool ID, allocation ID, public route, storage route, HTTP status, and round-trip duration.

### 4. Trace StorageApi hot-path services

Add parent operation spans in [AccessControlService.cs](ClientManager.StorageApi/Services/Implementations/AccessControlService.cs) and [ResourceAllocationService.cs](ClientManager.StorageApi/Services/Implementations/ResourceAllocationService.cs). Add child spans for configuration read, service/pool read, global rate-limit check, client rate-limit check, capacity checks, allocation write/release write, metrics emission, and usage recording.

Keep tags bounded and low-cardinality where possible. Client/service/pool IDs are useful for local analysis; avoid tagging full exception messages.

### 5. Trace rate-limit strategy work

Instrument [RateLimitService.cs](ClientManager.StorageApi/Services/Implementations/RateLimiting/RateLimitService.cs) and the strategy implementations under the rate-limiting strategy folder. Each strategy span should include strategy name, increment/peek mode, allowed/denied result, remaining requests, retry-after seconds, counter key count, and duration.

### 6. Trace document-store wrapper and storage response

Add a document-store instrumentation layer at the registration boundary in [StorageProviderRegistrationExtensions.cs](ClientManager.StorageApi/Utils/Extensions/StorageProviderRegistrationExtensions.cs), or instrument each implementation directly if a wrapper would fight keyed registrations. The span boundary must separate the logical storage wrapper call from the backend operation and include collection, operation, role, provider, success/failure, lock-wait time when available, and duration.

### 7. Add structured timing logs at meaningful levels

Use the existing [AppLogger.cs](ClientManager.Shared/Logging/AppLogger.cs) extra-data pattern. Log completion of access checks, acquires, releases, and storage operations with `DurationMs`, `Result`, and reason fields. Use Debug for normal timing flow, Info for policy denials, Warn for slow thresholds, and Error for actual exceptions.

## Verification

- `dotnet build .\ClientManager.slnx` completes without errors.
- `/prometheus/otel` on Api and StorageApi still responds.
- With no OTLP collector configured, both apps start and run without exporter exceptions.
- With `Observability:OtlpEndpoint` configured to a local collector, a single access check produces a trace containing public API, HttpClient, StorageApi, rate-limit, and storage spans.
- Logs for one access check include correlation ID plus non-empty trace/span IDs.
- Logs for one allocation acquire include storage write duration and counter update timing.
- **UI: Navigate to `/` — verify dashboard data still loads while tracing is enabled.**
- **UI: Navigate to `/monitor` — verify charts update under live traffic and take a screenshot showing no error banners.**
- **UI: Navigate to `/allocations` — acquire and release at least one allocation through API traffic, then verify the page reflects current allocation state.**
