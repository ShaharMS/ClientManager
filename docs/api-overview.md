# API overview

All routes use URL versioning: `/api/v1/...`. The default version is `1.0`.

**Interactive reference:** [http://localhost:5062/docs](http://localhost:5062/docs) (Swagger UI) — always the most up-to-date catalog of request/response schemas.

Errors return [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) `application/problem+json` with a `traceId` for log correlation. The same fields are echoed in `X-Problem-Title`, `X-Problem-Detail`, `X-Trace-Id`, and `X-Problem-Json` headers so nginx `auth_request` and similar proxies can forward denials without reading the subrequest body.

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
| `PUT` | `/{id}` | Entity | Full document replace |
| `PATCH` | `/` | Array of `{ id, …fields }` | Partial update (bulk or single); per-item results |
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

**Bulk client permission edits** can also go through `PATCH /api/v1/clients` with `services` / `resourcePools` / `globalRateLimit` in each patch object (deep-merged by dictionary key).

### PATCH semantics

- Each array item must include `id` plus only the fields to change.
- Unknown property names fail that item; `createdAt` cannot be patched.
- Update only — missing IDs return a per-item failure (use `POST` to create).
- Response body is always a `results` array; HTTP status reflects the batch outcome.

**HTTP status codes**

| Status | When |
| --- | --- |
| `200` | Every item in `results` has `status: updated` |
| `207` | Mixed — at least one `updated` and at least one `failed` |
| `422` | Every item `failed` (body still contains per-item `error` details) |
| `400` | Body missing or not a JSON array |
| `503` | Storage unavailable (whole request fails) |

**Per-item failures** — inside `results`, failed items include `error` (`ProblemResponse`):

| `error.status` | Typical cause |
| --- | --- |
| `404` | No entity with that `id` |
| `400` | Unknown property, missing `id`, or non-patchable field |
| `500` | Unexpected error applying that item |

Example:

```json
PATCH /api/v1/services
[
  { "id": "billing-api", "name": "Billing API" }
]
```

## Seeding

Base path: `/api/v1/seed` (Swagger tag: **Seeding**). Exports and imports catalog and optional statistics. Catalog-only export returns JSON in the `SeedOptions` shape (compatible with appsettings `Seed`). Requests that include `usageSnapshots` or `format=ndjson` stream `seed.ndjson`.

| Method | Path | Query | Body | Purpose |
| --- | --- | --- | --- | --- |
| `GET` | `/api/v1/seed` | `include`, `format` | — | Export (JSON or NDJSON file) |
| `DELETE` | `/api/v1/seed` | `include`, `format` | — | Wipe included collections |
| `POST` | `/api/v1/seed` | `include` | JSON or NDJSON | Import into empty collections |
| `PUT` | `/api/v1/seed` | `include`, `strategy=skip\|replace` | JSON or NDJSON | Merge into existing data |

**HTTP status codes (seed endpoints)**

| Status | When |
| --- | --- |
| `200` | Export or import succeeded |
| `400` | Unknown `include` collection, invalid `strategy` (PUT only), or missing/malformed body (POST/PUT) |
| `409` | Included collection not empty on POST, or another seed operation in progress |
| `503` | Storage unavailable |

Unlike PATCH, seed import does not return per-item failures in a `200` body — a storage or validation error fails the entire request.

See [Seed system](core/seed-system.md) and [Storage migration](migration/storage-migration.md) for workflows and curl examples.

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
| `GET` | `/api/v1/metrics/prometheus` | Prometheus text — usage and pool gauges |
| `GET` | `/api/v1/metrics/grafana` | Grafana-oriented JSON |
| `GET` | `/prometheus/otel` | Prometheus text — OpenTelemetry runtime metrics |

See the [Metrics integration guide](metrics-integration-guide.md) for Prometheus scrape jobs, the full metric catalog, OTLP trace setup, and example alerts.

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
