# Plan: ClientManager Resource & Access Control System

## Status: 🔲 Not started

## Overview

ClientManager is an internal API service that governs client access to other services in a microservice architecture. It provides three core capabilities:

1. **Access Control** — A deny-by-default whitelist model that determines which clients (identified by a string name) can access which services.
2. **Rate Limiting** — Enforces request-rate policies at three scopes: per-client-per-service, globally per-client, and **globally per-service/resource-pool** (catch-all aggregate limits protecting services from combined load across all clients). Uses configurable strategies (fixed window, sliding window, token bucket).
3. **Resource Allocation** — Manages named resource pools (e.g. S3 connections, DB connections) where clients must explicitly acquire and release slots, with TTL-based auto-expiry as a safety net. Resource pools also support global aggregate rate limits.

The system uses a **client-centric configuration model** — each client is a single document keyed by its ID, with all settings nested: which services it can access, per-service rate limits, resource pool quotas, and global-limit participation flags. System-wide entities (Service definitions, ResourcePool definitions, GlobalRateLimits) remain separate.

The system is designed for horizontal scalability — all state lives in shared storage (Redis or MongoDB) rather than in-process memory. A persistence abstraction supports three backends: **JSON file** (for development), **MongoDB**, and **Redis**. The data access layer lives in a separate `ClientManager.DataAccess` class library with interfaces and provider implementations. Shared entity models and enums live in `ClientManager.Shared`, referenced by all other projects. Services and clients can be defined via static config or managed dynamically through an Admin API.

The solution is a **monorepo with separate executables**: `ClientManager.Api` (the REST API service) and `ClientManager.AdminUI` (a Blazor Server admin dashboard). The admin UI communicates with the API exclusively via HTTP — it does not share service implementations, only entity types from `ClientManager.Shared`.

Other internal services call ClientManager's REST API to check if a client should be allowed through (or receive a 429), and to acquire/release resource slots before accessing capacity-constrained systems.

The current state is a bare ASP.NET Core 9.0 Web API with only the default WeatherForecast scaffolding. The desired end state is the full ClientManager system described above, with a Blazor Server admin dashboard for managing all entities and monitoring resource utilization.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 0 | [client-manager-0-project-rename.md](client-manager-0-project-rename.md) | Rename `ClientManager` project to `ClientManager.Api` for multi-project clarity |
| 1 | [client-manager-1-foundation.md](client-manager-1-foundation.md) | Client-centric domain records, enums, exception types, system-wide entities, and all interfaces |
| 2 | [client-manager-2-persistence.md](client-manager-2-persistence.md) | Repository implementations for JSON file, MongoDB, and Redis providers |
| 3 | [client-manager-3-rate-limiting.md](client-manager-3-rate-limiting.md) | Fixed window, sliding window, and token bucket rate limiting strategies |
| 4 | [client-manager-4-resource-allocation.md](client-manager-4-resource-allocation.md) | Resource pool management with per-client slot caps, acquire/release/TTL |
| 5 | [client-manager-5-access-control.md](client-manager-5-access-control.md) | Deny-by-default permission checks and accessibility report service |
| 6 | [client-manager-6-api-endpoints.md](client-manager-6-api-endpoints.md) | Client config CRUD with sub-resources + system-wide admin + operational endpoints |
| 7 | [client-manager-7-logging.md](client-manager-7-logging.md) | NLog structured logging infrastructure, Activity/correlation IDs, scopes |
| 8 | [client-manager-8-statistics-collection.md](client-manager-8-statistics-collection.md) | OpenTelemetry meters, request/rate-limit/allocation counters and histograms |
| 9 | [client-manager-9-middlewares.md](client-manager-9-middlewares.md) | RequestTrackingMiddleware and ErrorHandlingMiddleware |
| 10 | [client-manager-10-statistics-endpoints.md](client-manager-10-statistics-endpoints.md) | JSON statistics controller + OpenTelemetry Prometheus exporter endpoint |
| 11 | [client-manager-11-startup-config.md](client-manager-11-startup-config.md) | DI wiring, appsettings schema, provider selection, NLog, middlewares, OpenTelemetry, TTL cleanup |
| 12 | [client-manager-12-admin-ui-foundation.md](client-manager-12-admin-ui-foundation.md) | Blazor Server admin UI project, layout, navigation, typed HTTP client services |
| 13 | [client-manager-13-admin-ui-pages.md](client-manager-13-admin-ui-pages.md) | Admin UI pages: dashboard, client/service/pool/rate-limit CRUD, allocation monitoring |

## Key Decisions

- **Client-centric configuration model** — Each client is a single `ClientConfiguration` document keyed by `Id`. All per-client settings are nested: service access (with per-service rate limits and global-limit participation), resource pool quotas, and an optional global rate limit across all services. This replaces the previous separate `Client`, `AccessRule`, and `RateLimitPolicy` entities.
- **Deny-by-default access model** — A client must have a `ServiceAccessSettings` entry for a service in its configuration to be allowed. Missing entries or `IsAllowed: false` means denied.
- **Rate limit scoping: three levels** — (1) Per-client-per-service (nested in `ServiceAccessSettings.RateLimit`), (2) global per-client across all services (`ClientConfiguration.GlobalRateLimit`), and (3) global per-service/resource-pool catch-all (`GlobalRateLimit` entity). All applicable limits are evaluated; the most restrictive applies.
- **Global catch-all limits remain system-wide** — Each service and resource pool can have a `GlobalRateLimit` that caps aggregate traffic from all contributing clients. These are separate entities, not nested in client configs.
- **Client global-limit participation is configurable** — Each client has blanket defaults (`ContributesToGlobalLimits`, `ExemptFromGlobalLimits`) on `ClientConfiguration`, overridable per-service in `ServiceAccessSettings` (`ContributesToGlobalLimit`, `ExemptFromGlobalLimit`). A client that doesn't contribute won't increment the global counter; a client that's exempt won't be denied by it.
- **Per-client resource pool quotas** — Each client can have a `MaxSlots` cap per resource pool (nested in `ClientConfiguration.ResourcePools`), independent of the system-wide pool's `MaxSlots`. The effective limit is `min(client cap, remaining pool capacity)`.
- **Three rate-limit strategies coexist** — Fixed window, sliding window, and token bucket. Each rate limit config specifies which strategy to use. Different clients/services can use different strategies. Global limits use the same strategies.
- **System-wide entities stay separate** — `Service`, `ResourcePool`, and `GlobalRateLimit` definitions are independent entities, not nested in client configs. They define what exists in the system; client configs define what each client can use.
- **Extensible resource pool model** — Resource pools are generic named entities (e.g. `s3-connections`, `database-pool-x`) with a configurable max-slot count. Not hardcoded to S3.
- **Resource allocation: explicit free + TTL safety net** — Clients call acquire/release, but allocations auto-expire after a configurable TTL to prevent leaks from crashed clients.
- **Persistence abstraction with three providers** — Data access interfaces and implementations live in `ClientManager.DataAccess` (separate class library). JSON file (dev), MongoDB, and Redis provider implementations. Provider selected via config at startup.
- **Shared types in `ClientManager.Shared`** — Entity models (`ClientConfiguration`, `Service`, `ResourcePool`, etc.) and enums (`RateLimitStrategy`, `GlobalRateLimitTarget`, `PersistenceProvider`) live in `ClientManager.Shared`. All other projects reference this library for shared types. `ClientManager.DataAccess` contains only abstractions (interfaces) and database implementations.
- **Horizontally scalable** — No in-process state for rate limiting or resource allocation. All counters and allocations live in the shared persistence layer with atomic operations.
- **No auth now, extensible later** — No authentication middleware is added, but the middleware pipeline and controller structure don't block adding API key or JWT auth later.
- **MongoDB.Driver directly** — No EF Core; use the official MongoDB C# driver for the MongoDB persistence provider.
- **REST API only** — No client SDK/NuGet package. Services call HTTP endpoints directly.
- **Records for data types** — All data-only types (entities, DTOs, responses, results) use `record` or `record struct` instead of `class`. Small single-field value types use `record struct`. Service classes, middleware, controllers, and options-binding types stay as `class`. Exceptions stay as `class` (they extend `Exception`).
- **NLog structured logging** — All logging uses NLog with structured message templates: `Logger.Info("Static message | {@Properties}", new { Field1, Field2 })`. NLog targets configured for Console, File, and Elasticsearch (all via nlog.config, no custom code). Uses `Activity` and `ScopeContext` for correlation IDs, latency tracking, and baggage.
- **Throw-based error handling** — Services throw typed exceptions for all non-success outcomes — both infrastructure faults (not found, conflict) and operational denials (access denied, rate limited, no slots). `ErrorHandlingMiddleware` catches all exceptions and maps them to HTTP status codes. Controllers only handle the success path and never manually construct error responses.
- **Exception types** — `NotFoundException` → 404, `ConflictException` → 409, `ValidationException` → 400, `ClientDisabledException` → 403, `AccessDeniedException` → 403, `RateLimitedException` → 429 (with `Retry-After` header). All live in `ClientManager.Api/Models/Exceptions/`.
- **Passthrough HTTP status codes** — Operational endpoints return the actual HTTP status codes that calling APIs can directly bubble up to their own callers. A rate-limited client gets a 429 (not a 200 with a flag). An unauthorized client gets a 403. A missing service gets a 404. This means calling APIs can treat ClientManager responses as passthrough: success = 200 (proceed), any error = throw/forward the status code upstream without interpretation. This eliminates the need for callers to inspect response bodies to decide what HTTP status to return to their own users.
- **Request tracking middleware** — `RequestTrackingMiddleware` wraps the entire pipeline, starts `Activity`, extracts clientId/serviceId, records OpenTelemetry metrics (counters, histograms), and logs structured request data. Feeds both Prometheus and JSON statistics endpoints.
- **Statistics endpoints** — Both human-readable JSON (`/api/statistics/...`) and Prometheus-compatible (`/metrics`) endpoints expose request counts, latencies, rate limit hits, and allocation stats per clientId, serviceId, and clientId+serviceId.
- **OpenTelemetry for metrics** — Uses `OpenTelemetry.Exporter.Prometheus.AspNetCore` for Prometheus `/metrics` endpoint. Custom `Meter` instances define counters and histograms. No separate Prometheus client library needed.
- **Project naming: `ClientManager.Api`** — The API project is named `ClientManager.Api` (not `ClientManager`) to disambiguate from the solution name and other projects. Renamed in step 0 before any implementation begins.
- **Monorepo with separate executables** — `ClientManager.Api` and `ClientManager.AdminUI` are separate .NET projects in the same solution, each producing their own executable. They share `ClientManager.Shared` for entity types but are deployed independently.
- **Blazor Server for Admin UI** — The admin dashboard uses Blazor Web App with interactive server render mode. All rendering happens server-side over a SignalR connection. No separate JavaScript/TypeScript toolchain required.
- **Admin UI consumes REST API via HTTP** — The admin UI does not reference `ClientManager.Api` or share service implementations. It calls the API's REST endpoints using typed `HttpClient` services, maintaining a clean API boundary.
