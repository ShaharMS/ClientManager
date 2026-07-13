# Metrics catalog

Prometheus scrape path: `GET /prometheus/otel`  
Job name (local compose): `clientmanager-api`

OpenTelemetry instrument names use dots; Prometheus export uses underscores, `_total` on counters, and `_milliseconds_bucket` on histograms.

## Counters

| Prometheus name | OTel name | Labels | Description |
| --- | --- | --- | --- |
| `clientmanager_requests_total` | `clientmanager.requests` | `service`, `client`, `outcome` | Access-check outcomes |
| `clientmanager_http_requests_total` | `clientmanager.http.requests` | `method`, `endpoint`, `statusCode` | All HTTP requests |
| `clientmanager_http_requests_errors_total` | `clientmanager.http.requests.errors` | `method`, `endpoint`, `statusCode` | HTTP status ≥ 400 |
| `clientmanager_access_denied_total` | `clientmanager.access.denied` | `clientId`, `serviceId`, `reason` | Access denials |
| `clientmanager_ratelimit_allowed_total` | `clientmanager.ratelimit.allowed` | `clientId`, `serviceId` | Rate-limit pass |
| `clientmanager_ratelimit_denied_total` | `clientmanager.ratelimit.denied` | `clientId`, `serviceId` | Rate-limit fail |
| `clientmanager_ratelimit_global_hits_total` | `clientmanager.ratelimit.global_hits` | `serviceId` | Service-global limit denials |

### `outcome` values (`clientmanager_requests_total`)

`granted`, `client_not_found`, `service_not_found`, `not_configured`, `client_disabled`, `service_disabled`, `not_allowed`, `global_rate_limited`, `rate_limited`

## Histograms (milliseconds)

| Prometheus prefix | Labels | Description |
| --- | --- | --- |
| `clientmanager_http_requests_duration_milliseconds_*` | `method`, `endpoint` | HTTP middleware timing |
| `clientmanager_storage_access_duration_milliseconds_*` | `clientId`, `serviceId`, `result`, `reason` | Access-check path |
| `clientmanager_storage_ratelimit_strategy_duration_milliseconds_*` | `strategy`, `mode`, `counter_key_count`, `result` | Strategy evaluation |
| `clientmanager_storage_document_store_duration_milliseconds_*` | `collection`, `operation`, `role`, `provider`, `result` | Document store ops |

## ASP.NET Core (automatic)

`http_server_request_duration_seconds_*`, `http_server_active_requests`, etc. (package version dependent)

## Cardinality estimator

Rough active series per counter/histogram family:

```
requests_series  ≈ active_services × active_clients × outcomes
http_series      ≈ methods × endpoints × status_codes
storage_series   ≈ active_client_service_pairs × reasons × ...
```

Example: 50 services × 200 clients × 9 outcomes ≈ **90k** series for `clientmanager_requests_total` alone. Share this estimate with your platform team before enabling high-cardinality labels in prod.

## Not on Prometheus

| Signal | Source |
| --- | --- |
| Admin UI RPM card | `GET /api/v1/statistics/overview` (shared Redis ring) |

Dashboard RPM equivalent uses `sum(rate(clientmanager_requests_total{outcome="granted"}[5m])) * 60` from per-pod counters.

## Prod scrape checklist

1. Scrape `/prometheus/otel` — not admin or statistics JSON routes.
2. Prefer per-pod targets; avoid round-robin single URL.
3. Sticky single target: OK for SLO ratios; global volume panels are partial.
4. Restrict scrape network — endpoint has no auth.

## Dashboard import

File: [`observability/grafana/dashboards/clientmanager.json`](../observability/grafana/dashboards/clientmanager.json)

Variables: `datasource`, `pod`, `service`, `client`. Only the **Pod** zone reacts to `$pod`.
