# API overview

All routes use URL versioning: `/api/v1/...`. The default version is `1.0`.

**Interactive reference:** [http://localhost:5062/docs](http://localhost:5062/docs) (Swagger UI) — always the most up-to-date catalog of request/response schemas.

Errors return [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) `application/problem+json` with a `traceId` for log correlation.

## Runtime gatekeeping

These endpoints are designed for reverse proxies (`auth_request`), application middleware, and workers. Runtime gatekeeping uses **GET with query parameters** so nginx and similar tools can call them without a request body.

| Method | Path | Query parameters | Side effects |
| --- | --- | --- | --- |
| `GET` | `/api/v1/access/check` | `clientId`, `serviceId` | Increments rate limits; records `Granted` or `Denied` usage |
| `GET` | `/api/v1/access/{clientId}` | — | Read-only accessibility report across all services |
| `GET` | `/api/v1/resources/acquire` | `clientId`, `resourcePoolId` | Creates allocation; increments counters |
| `GET` | `/api/v1/resources/release` | `allocationId` | Frees allocation; records `Released` usage |

See [Request flow](core/request-flow.md) and [Integration guide](integration-guide.md) for pipeline order, status codes, and nginx examples.

## Catalog CRUD

Catalog controllers share a common pattern:

| Method | Path suffix | Body | Purpose |
| --- | --- | --- | --- |
| `POST` | `/search` | `DocumentQuery` (optional) | Filtered, sorted, paginated list |
| `GET` | `/{id}` | — | Get by ID |
| `POST` | `/` | Entity | Create (409 if ID exists) |
| `PUT` | `/{id}` | Entity | Update |
| `DELETE` | `/{id}` | — | Delete |

### Top-level resources

| Base path | Entity | Tag |
| --- | --- | --- |
| `/api/v1/clients` | `ClientConfiguration` | Client Configurations |
| `/api/v1/services` | `Service` | Services |
| `/api/v1/resource-pools` | `ResourcePool` | Resource Pools |
| `/api/v1/global-rate-limits` | `GlobalRateLimit` | Global Rate Limits |

### Nested client settings

Under `/api/v1/clients/{id}/`:

| Path | Methods | Purpose |
| --- | --- | --- |
| `services` | `GET` | Paginated list of service access settings |
| `services/{serviceId}` | `GET`, `PUT`, `DELETE` | Per-service `ServiceAccessSettings` |
| `resource-pools` | `GET` | Paginated list of per-client pool quotas |
| `resource-pools/{poolId}` | `GET`, `PUT`, `DELETE` | Per-client `ResourcePoolSettings` (`maxSlots`) |
| `global-rate-limit` | `GET`, `PUT`, `DELETE` | Client-wide `globalRateLimit` |

The Admin UI uses these nested routes when editing individual cards on the client editor.

## Statistics

Base path: `/api/v1/statistics`

### Overview and summaries

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/overview` | Counts of clients, services, pools, active allocations |
| `POST` | `/clients/search` | Paginated per-client summary statistics |
| `GET` | `/clients/{clientId}` | Detailed stats for one client |
| `GET` | `/global-usage` | System-wide usage rollup |
| `GET` | `/client-summaries` | Compact client usage summaries |

### Usage time series

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/usage-timeseries` | Time-bucketed usage for charts |
| `GET` | `/client-usage-breakdown` | Per-client usage split |
| `GET` | `/historical-usage` | Historical rollup |
| `GET` | `/historical-usage/by-client` | Historical usage per client |

### Catalog-enriched statistics

| Method | Path | Description |
| --- | --- | --- |
| `POST` | `/services/search` | Services with usage stats |
| `GET` | `/services/{serviceId}` | One service with stats |
| `POST` | `/resource-pools/search` | Pools with usage stats |
| `GET` | `/resource-pools/{resourcePoolId}` | One pool with stats |

Statistics endpoints are read-only and safe to poll for dashboards.

## Metrics export

| Method | Path | Format |
| --- | --- | --- |
| `GET` | `/api/v1/metrics/prometheus` | Prometheus text exposition |
| `GET` | `/api/v1/metrics/grafana` | Grafana-oriented JSON |

Additionally, OpenTelemetry metrics are scraped at:

| Path | Format |
| --- | --- |
| `/prometheus/otel` | Prometheus (OTEL exporter) |

## Infrastructure endpoints

| Path | Purpose |
| --- | --- |
| `/docs` | Swagger UI |
| `/swagger/v1/swagger.json` | OpenAPI document |

There is **no** dedicated `/health` or `/ready` endpoint today. For load balancers, common choices are probing `/api/v1/statistics/overview` or adding a health check in a future change.

## Search queries

`POST …/search` endpoints accept a `DocumentQuery` body:

```json
{
  "filters": [],
  "sort": [],
  "skip": 0,
  "take": 100
}
```

Pass `null` or `{}` for an unpaginated "all" query where the controller allows it. See Swagger for filter field names per entity.

## Versioning

Configured in `ApiVersioning:DefaultVersion` (default `1.0`). The URL segment `v1` maps to version 1.0. Clients should include the version in the path.

## Related reading

- [Integration guide](integration-guide.md) — edge integration patterns
- [Usage and observability](core/usage-and-observability.md) — how events become statistics
- [Admin UI guide](admin-ui-guide.md) — which pages call which endpoints
