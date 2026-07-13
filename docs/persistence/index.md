# Persistence overview

ClientManager binds one persistence **provider** to each **storage role** at startup. Every repository in that role uses the same backend.

The Admin UI never talks to storage directly — it calls the API, which uses in-process storage under `ClientManager.Api/Storage`.

The supported providers are **MongoDB** and **Redis**.

## Storage roles

| Role | What it stores | Typical access pattern |
| --- | --- | --- |
| `Configuration` | Clients, services, global rate limits | Read-heavy on access checks; occasional CRUD |
| `RateLimiting` | Fixed-window, sliding-window, and token-bucket counters | Very high write rate; short TTL |
| `Rpm` | Global RPM second-bucket ring | Moderate write rate; shared across replicas |

Concrete collections:

| Role | Collections / keys |
| --- | --- |
| `Configuration` | `ClientConfiguration`, `services`, `GlobalRateLimit` |
| `RateLimiting` | Rate-limit counter keys |
| `Rpm` | Second-bucket RPM ring keys |

## How configuration works

```text
DefaultProvider + Default* settings  →  fallback for every role
Roles.{Role}.Provider + role options →  override one role only
```

Example — Redis for hot paths, MongoDB for catalog:

```json
{
  "Persistence": {
    "DefaultProvider": "MongoDb",
    "DefaultMongoDb": {
      "ConnectionString": "mongodb://mongo:27017",
      "DatabaseName": "ClientManager"
    },
    "Roles": {
      "RateLimiting": {
        "Provider": "Redis",
        "Redis": { "Host": "redis", "Port": 6379, "DatabaseIndex": 1 }
      },
      "Rpm": {
        "Provider": "Redis",
        "Redis": { "Host": "redis", "Port": 6379, "DatabaseIndex": 2 }
      }
    }
  }
}
```

Environment variables use `Section__SubSection__Property` (double underscore). See [Configuration reference](../configuration-reference.md#persistence).

## Provider comparison

| Provider | Best for | Multi-instance prod |
| --- | --- | --- |
| [MongoDB](mongodb.md) | Durable shared documents, catalog | Recommended for `Configuration` |
| [Redis](redis.md) | Atomic counters, rate limits, RPM ring | Recommended for `RateLimiting` and `Rpm` |

### Mental model

1. **Counters and RPM** → Redis (`RateLimiting`, `Rpm`) in production.
2. **Durable catalog** → MongoDB (`Configuration`) or Redis for minimal setups.
3. **Mixed roles** are normal: override only roles that benefit from a different backend.

## Suggested layouts

### Local development

| Role | Provider | Notes |
| --- | --- | --- |
| All | `Redis` | Default in `appsettings.json`; single Redis instance with default DB index |

Seed via `seed_data.py` or `GET`/`POST /api/v1/seed` with `Seed:SeedApiEnabled: true`.

### Production — balanced (recommended)

| Role | Provider | Why |
| --- | --- | --- |
| `Configuration` | MongoDB | Shared, durable, queryable catalog |
| `RateLimiting` | Redis | Native atomic counters + TTL |
| `Rpm` | Redis | Fast shared RPM ring across replicas |

Use separate Redis `DatabaseIndex` per role on one server.

### Production — minimal ops

| Role | Provider |
| --- | --- |
| All | Redis or all MongoDB |

Works for smaller deployments. Separate Redis DB indexes per role when using Redis for everything.

## Provider guides

| Guide | When to read |
| --- | --- |
| [MongoDB](mongodb.md) | Production durable catalog |
| [Redis](redis.md) | Rate limits, RPM ring, minimal all-in-Redis setups |

## Related reading

- [Configuration reference — Persistence](../configuration-reference.md#persistence)
- [Architecture — storage layering](../core/architecture.md)
- [Usage and observability](../core/usage-and-observability.md) — RPM role
