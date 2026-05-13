# Hot Path Performance Agent Findings

This file summarizes the read-only subagent findings that informed the plan.

## Access Checks

Call chain:
- [AccessCheckController.cs](ClientManager.Api/Controllers/AccessCheckController.cs)
- [AccessControlService.cs](ClientManager.Api/Services/Implementations/AccessControlService.cs)
- [RuntimeStateClient.cs](ClientManager.Api/Services/InternalClients/Implementations/RuntimeStateClient.cs)
- [AccessCheckController.cs](ClientManager.StorageApi/Controllers/AccessCheckController.cs)
- [AccessControlService.cs](ClientManager.StorageApi/Services/Implementations/AccessControlService.cs)
- [RateLimitService.cs](ClientManager.StorageApi/Services/Implementations/RateLimiting/RateLimitService.cs)
- [RateLimitStateDatabase.cs](ClientManager.DataAccess/Databases/Implementations/RateLimitStateDatabase.cs)

Likely bottlenecks:
- Public API to StorageApi HTTP hop on every request.
- Client configuration and service reads on every access check, mitigated only by existing catalog cache patterns.
- Multiple rate-limit counter operations per request, especially token bucket read/write pairs.
- Sequential `GetMultipleCountsAsync`/`SetMultipleCountsAsync` loops in rate-limit state storage.
- Hot-path logging/metrics allocations that should remain bounded and visible in traces.

## Resource Allocation

Call chain:
- [ResourceAllocationController.cs](ClientManager.Api/Controllers/ResourceAllocationController.cs)
- [ResourceAllocationService.cs](ClientManager.Api/Services/Implementations/ResourceAllocationService.cs)
- [RuntimeStateClient.cs](ClientManager.Api/Services/InternalClients/Implementations/RuntimeStateClient.cs)
- [ResourceAllocationController.cs](ClientManager.StorageApi/Controllers/ResourceAllocationController.cs)
- [ResourceAllocationService.cs](ClientManager.StorageApi/Services/Implementations/ResourceAllocationService.cs)
- [ResourceAllocationDatabase.cs](ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs)
- [JsonFileDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs)

Likely bottlenecks:
- Acquire performs pool read, config read, client active-count read, global rate-limit work, pool active-count read, allocation write, and two counter increments.
- Release reads the allocation once in service code and again in database code before writing release state and decrementing two counters.
- Background cleanup and statistics scans can compete with hot-path storage if allocation collections grow.

## Storage

Likely bottlenecks:
- JsonFile rewrites full collection or counter files under a per-instance lock.
- Multiple JsonFile store instances can share the same file path with separate locks, causing `_counters.json.tmp` collisions.
- Lucene currently falls back to in-memory query evaluation for `SearchAsync` and commits per write/counter update.
- Redis and MongoDB lack batch counter APIs at the abstraction layer even though their backends can support them efficiently.

## Observability

Existing assets:
- [RequestTrackingMiddleware.cs](ClientManager.Api/Middlewares/RequestTrackingMiddleware.cs) and [RequestTrackingMiddleware.cs](ClientManager.StorageApi/Middlewares/RequestTrackingMiddleware.cs) record request durations.
- [ClientManagerMetrics.cs](ClientManager.Api/Utils/Instrumentation/ClientManagerMetrics.cs) and [StorageApiMetrics.cs](ClientManager.StorageApi/Utils/Instrumentation/StorageApiMetrics.cs) expose counters and request histograms.
- [nlog.config](ClientManager.Api/nlog.config) and [nlog.config](ClientManager.StorageApi/nlog.config) already include trace/span/correlation fields.

Missing assets:
- ActivitySource definitions and `.WithTracing(...)` host configuration.
- HttpClient instrumentation for public API to StorageApi calls.
- Operation-level histograms for access checks, acquires, releases, rate-limit strategy work, and document-store operations.
- Span boundaries that distinguish storage wrapper calls from actual backend operations.
