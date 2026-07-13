# API overview

All routes use URL versioning: `/api/v1/...`.

**Interactive reference:** [http://localhost:5062/docs](http://localhost:5062/docs) (Swagger UI).

Errors return [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) `application/problem+json` with a `traceId`. The same fields are echoed in `X-Problem-Title`, `X-Problem-Detail`, `X-Trace-Id`, and `X-Problem-Json` headers so nginx `auth_request` can forward denials without reading the subrequest body.

## Runtime gatekeeping

Designed for reverse proxies (`auth_request`), application middleware, and workers. Uses **GET with query parameters**.

| Method | Path | Query parameters | Side effects |
| --- | --- | --- | --- |
| `GET` | `/api/v1/access/check` | `clientId`, `serviceId` | Increments rate limits; records RPM; emits OTel metrics |

See [Request flow](core/request-flow.md) and [Integration guide](integration-guide.md) for pipeline order, status codes, and nginx examples.

!!! note "Removed runtime routes"
    `GET /api/v1/access/{clientId}`, `GET /api/v1/resources/acquire`, and `GET /api/v1/resources/release` were removed.

## Catalog CRUD

Catalog controllers share a common pattern:

| Method | Path suffix | Body | Purpose |
| --- | --- | --- | --- |
| `POST` | `/search` | `DocumentQuery` (optional) | Filtered, sorted, paginated list |
| `GET` | `/{id}` | — | Get by ID |
| `POST` | `/` | Entity | Create (409 if ID exists) |
| `PUT` | `/{id}` | Entity | Full document replace |
| `DELETE` | `/{id}` | — | Delete |

`PATCH` and bulk partial updates were removed.

### Top-level resources

| Base path | Entity | Tag |
| --- | --- | --- |
| `/api/v1/clients` | `ClientConfiguration` | Client Configurations |
| `/api/v1/services` | `Service` | Services |
| `/api/v1/global-rate-limits` | `GlobalRateLimit` | Global Rate Limits |

`GlobalRateLimit.id` is the service ID; rate-limit fields live in nested `policy`.

### Removed catalog routes

- `/api/v1/resource-pools`
- Nested `/api/v1/clients/{id}/services/...`, `resource-pools/...`, `global-rate-limit`

Edit per-client service access through the full `ClientConfiguration` document (`PUT /api/v1/clients/{id}`).

## Seeding

Base path: `/api/v1/seed`. Gated by `Seed:SeedApiEnabled` (HTTP 404 when `false`).

| Method | Path | Query | Body | Purpose |
| --- | --- | --- | --- | --- |
| `GET` | `/api/v1/seed` | `include` | — | Export JSON `SeedOptions` |
| `DELETE` | `/api/v1/seed` | `include` | — | Wipe included collections |
| `POST` | `/api/v1/seed` | `include` | JSON | Import into empty collections |
| `PUT` | `/api/v1/seed` | `include`, `strategy=skip\|replace` | JSON | Merge into existing data |

| Status | When |
| --- | --- |
| `200` | Export or import succeeded |
| `400` | Unknown `include`, invalid `strategy`, or malformed body |
| `404` | `SeedApiEnabled` is `false` |
| `409` | Collection not empty on POST, or operation in progress |
| `503` | Storage unavailable |

## Statistics

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/v1/statistics/overview` | Client count, service count, current RPM |

## Metrics export

| Method | Path | Format |
| --- | --- | --- |
| `GET` | `/prometheus/otel` | Prometheus text — OpenTelemetry runtime metrics |

See the [Metrics integration guide](metrics-integration-guide.md).

## Infrastructure endpoints

| Path | Purpose |
| --- | --- |
| `/docs` | Swagger UI |
| `/swagger/v1/swagger.json` | OpenAPI document |

There is **no** dedicated `/health` endpoint. Common choices: probe `/api/v1/statistics/overview` or add a health check later.

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

Pass `null` or `{}` for an unpaginated "all" query where allowed.

## Related reading

- [Integration guide](integration-guide.md) — edge integration patterns
- [Usage and observability](core/usage-and-observability.md) — RPM and metrics
- [Admin UI guide](admin-ui-guide.md) — which pages call which endpoints
