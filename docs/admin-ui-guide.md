# Admin UI guide

The Admin UI (`ClientManager.AdminUI`) is a **Blazor Server** application using **Radzen** components. It is a thin HTTP client over the same API that integrators call.

**URL:** [http://localhost:5100](http://localhost:5100) (local default)

If the API is down, every page fails to load data.

## Navigation map

| Route | Page | What it manages |
| --- | --- | --- |
| `/` | Dashboard | Three stat cards: clients, services, RPM |
| `/clients` | Clients | `ClientConfiguration` documents |
| `/clients/new`, `/clients/{id}` | Client editor | Per-client settings (see below) |
| `/services` | Services | Service catalog |
| `/services/new`, `/services/{id}` | Service editor | Create/edit a protected capability |
| `/rate-limits` | Rate limits | Global limits (`GlobalRateLimit`, one per service) |
| `/rate-limits/new`, `/rate-limits/{id}` | Rate limit editor | Aggregate throughput cap for a service |
| `/settings` | Settings | Language and UI preferences (browser local storage) |

## Client editor sections

When editing `/clients/{id}`:

| Card | Domain fields | Effect |
| --- | --- | --- |
| **Basic config** | `id`, `name`, `isEnabled`, `contributesToGlobalLimits`, `exemptFromGlobalLimits` | Master on/off and global-limit participation |
| **Global rate limit** | `globalRateLimit` | Blanket per-client request cap across all services |
| **Service access** | `services` dictionary | Per-service `isAllowed`, `rateLimit`, global-limit overrides |

Deny-by-default: a client reaches a service only when an explicit `isAllowed: true` entry exists.

## Typical operator workflows

### Onboard a new integration (client)

1. **Services** — ensure the capability exists and `isEnabled` is true.
2. **Clients** → **Create** — set `clientId`, name, enable the client.
3. **Service access** card — add the service with `isAllowed: true`; optionally set a per-service rate limit.
4. Integrator edge layer — pass the same `clientId` on `GET /api/v2/access/check`.

### Cap aggregate load on a service

1. **Rate limits** → create a global limit for the service (`id` = service ID).
2. Tune `policy.strategy`, `policy.maxRequests`, and `policy.window`.
3. Watch **Dashboard** RPM while load testing.

### Debug unexpected denials

1. Check **Rate limits** for aggregate exhaustion on the service.
2. Open the client editor — verify `isEnabled`, service `isAllowed`, and per-client limits.
3. Correlate API `traceId` from the integrator's `problem+json` response with API logs and `/prometheus/otel` denial counters.

## Dashboard

The dashboard calls `GET /api/v2/statistics/overview` and polls every 10 seconds. It shows:

- Total clients (links to `/clients`)
- Total services (links to `/services`)
- Requests per minute (RPM from the shared bucket ring)

## Architecture notes for UI contributors

| Piece | Location | Role |
| --- | --- | --- |
| API services | `ClientManager.AdminUI/Services/*ApiService.cs` | HTTP mapping to Shared models |
| Page templates | `Components/Shared/` | Reusable list/editor layouts |
| User prefs | `UserPreferencesService` | Browser local storage |

The UI uses a named `HttpClient` (`ClientManagerApi`) configured with `ApiBaseUrl`.

## Localization

The Admin UI supports **English** and **Hebrew** (`he-IL`). Change language under **Settings → Language**.

Strings live in `ClientManager.AdminUI/Resources/SharedResources*.resx`. See [Localization](localization.md).

## Related reading

- [Domain model](core/domain-model.md) — entity relationships and limit precedence
- [API overview](api-overview.md) — catalog and statistics endpoints
- [Request flow](core/request-flow.md) — what access checks do
- [Getting started](getting-started.md) — run order and seed data
