# Admin UI guide

The Admin UI (`ClientManager.AdminUI`) is a **Blazor Server** application using **Radzen** components. It is a thin HTTP client over the same API that integrators call — it never touches `ClientManager.DataAccess` directly.

**URL:** [http://localhost:5100](http://localhost:5100) (local default)

If the API is down, every page fails to load data.

## Navigation map

| Route | Page | What it manages |
| --- | --- | --- |
| `/` | Dashboard | System overview, per-client usage charts, time-range filters |
| `/monitor` | Monitor | Live accessibility report per client across all services |
| `/allocations` | Active allocations | Who holds resource pool slots right now |
| `/clients` | Clients | `ClientConfiguration` documents |
| `/clients/new`, `/clients/{id}` | Client editor | Per-client settings (see below) |
| `/services` | Services | Service catalog (`Service` entities) |
| `/services/new`, `/services/{id}` | Service editor | Create/edit a protected capability |
| `/resource-pools` | Resource pools | Pool definitions (`maxSlots`, `allocationTtl`) |
| `/resource-pools/new`, `/resource-pools/{id}` | Pool editor | Create/edit a pool |
| `/rate-limits` | Rate limits | **Global** limits where `targetType = Service` |
| `/rate-limits/new`, `/rate-limits/{id}` | Rate limit editor | Aggregate throughput caps per service |
| `/quotas` | Quotas | **Global** limits where `targetType = ResourcePool` |
| `/quotas/new`, `/quotas/{id}` | Quota editor | Aggregate acquisition-rate caps per pool |
| `/settings` | Settings | Chart axis scale and UI preferences (stored in browser local storage) |

### Naming: rate limits vs quotas

Both screens edit `GlobalRateLimit` documents. The UI splits them by target:

- **Rate limits** → limits on **services** (request throughput across all clients)
- **Quotas** → limits on **resource pools** (how fast slots can be acquired)

See [Domain model](core/domain-model.md) for how global, client-wide, and per-service limits stack.

## Client editor sections

When editing `/clients/{id}`, cards map to `ClientConfiguration` fields:

| Card | Domain fields | Effect |
| --- | --- | --- |
| **Basic config** | `id`, `name`, `isEnabled`, `contributesToGlobalLimits`, `exemptFromGlobalLimits` | Master on/off switch and global-limit participation |
| **Global rate limit** | `globalRateLimit` | Blanket per-client request cap across all services |
| **Service access** | `services` dictionary | Per-service `isAllowed`, per-service `rateLimit`, global-limit overrides |
| **Resource pools** | `resourcePools` dictionary | Per-client `maxSlots` per pool (concurrency cap) |

Deny-by-default: a client reaches a service only when an explicit `isAllowed: true` entry exists. Missing entries cause `401` on access checks.

## Typical operator workflows

### Onboard a new integration (client)

1. **Services** — ensure the capability exists (e.g. `pdf-render`) and `isEnabled` is true.
2. **Clients** → **Create** — set `clientId`, name, enable the client.
3. **Service access** card — add the service with `isAllowed: true`; optionally set a per-service rate limit.
4. If the integration uses pools — set **Resource pools** card entries with `maxSlots`.
5. **Monitor** — confirm the client shows green for expected services.
6. Integrator edge layer — pass the same `clientId` on `GET /api/v1/access/check`.

### Cap aggregate load on a service

1. **Rate limits** → create a global limit on the service (`targetType: Service`).
2. Tune `strategy`, `maxRequests`, and `window`.
3. Use **Dashboard** global usage charts to validate before enforcing in production.

### Limit concurrent work in a pool

1. **Resource pools** — set system-wide `maxSlots` and `allocationTtl`.
2. **Clients** — set per-client `maxSlots` in the resource pools card.
3. **Quotas** (optional) — global acquisition-rate limit on the pool.
4. **Allocations** — watch active holders; integrators must call acquire/release from application code.

### Debug unexpected denials

1. **Monitor** — see which services are blocked and whether rate limits are the reason (read-only; does not consume quota).
2. Check **Rate limits** for aggregate exhaustion on the service.
3. Open the client editor — verify `isEnabled`, service `isAllowed`, and per-client limits.
4. Correlate API `traceId` from the integrator's `problem+json` response with API logs.

## Dashboard and charts

The dashboard calls `/api/v1/statistics/*` endpoints. Time range and chart preferences come from:

- Page controls (preset ranges)
- `/settings` (axis scale: linear vs logarithmic)

Charts poll on an interval. Heavy polling is safe for statistics endpoints; do **not** poll `GET /access/check` for monitoring — that consumes quota.

## Architecture notes for UI contributors

| Piece | Location | Role |
| --- | --- | --- |
| API services | `ClientManager.AdminUI/Services/*ApiService.cs` | HTTP mapping to Shared models |
| Chart builders | `Services/ChartData/` | Transform statistics responses for Radzen charts |
| Page templates | `Components/Shared/` | Reusable list/editor layouts |
| User prefs | `UserPreferencesService` | Browser local storage |

The UI uses a named `HttpClient` (`ClientManagerApi`) configured with `ApiBaseUrl`. In Development, TLS certificate validation is relaxed for local HTTPS experiments.

## Related reading

- [Domain model](core/domain-model.md) — entity relationships and limit precedence
- [API overview](api-overview.md) — catalog and statistics endpoints the UI calls
- [Request flow](core/request-flow.md) — what access checks and acquisitions do
- [Getting started](getting-started.md) — run order and seed data
