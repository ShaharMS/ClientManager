# Persistence overview

ClientManager does not pick a database per entity or per HTTP request. **`ClientManager.Api`** binds one persistence **provider** to each **storage role** at startup. Every repository in that role uses the same backend.

The Admin UI never talks to storage directly — it calls the API, which uses `ClientManager.DataAccess` in-process.

## Storage roles

| Role | What it stores | Typical access pattern |
| --- | --- | --- |
| `Configuration` | Clients, services, resource pools, global rate limits | Read-heavy on access checks; occasional CRUD |
| `RateLimiting` | Sliding/fixed-window and token-bucket counters | Very high write rate; short TTL |
| `Allocations` | Active allocation documents + pool counters | Moderate write rate; must be atomic |
| `Statistics` | Usage snapshot time-series (`UsageSnapshots`) | Append/merge; large range queries for dashboards |

Concrete collections:

| Role | Collections / keys |
| --- | --- |
| `Configuration` | `ClientConfiguration`, `services`, `resource_pools`, `GlobalRateLimit` |
| `RateLimiting` | Counter keys only (`usage:*`, rate-limit keys) |
| `Allocations` | `ResourceAllocation`, `alloc-count:*` counters |
| `Statistics` | `UsageSnapshots` |

## How configuration works

```text
DefaultProvider + Default* settings  →  fallback for every role
Roles.{Role}.Provider + role options →  override one role only
```

Example — Redis for hot paths, MongoDB for everything else:

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
      "Allocations": {
        "Provider": "Redis",
        "Redis": { "Host": "redis", "Port": 6379, "DatabaseIndex": 2 }
      }
    }
  }
}
```

Environment variables use `Section__SubSection__Property` (double underscore). See [Configuration reference](../configuration-reference.md#persistence).

## Provider comparison

| Provider | Best for | Weak at | Multi-instance prod |
| --- | --- | --- | --- |
| [JsonFile](json-file.md) | Local dev, zero deps, readable files | Large statistics files, full-collection scans | Not recommended (single-host semantics) |
| [SQLite](sqlite.md) | Large local histories, low memory vs JsonFile | Shared writes across many API replicas | Single instance only |
| [MongoDB](mongodb.md) | Durable shared documents, server-side queries | Counter hot paths without careful schema | Recommended default for prod catalog + stats |
| [Redis](redis.md) | Atomic counters, rate limits, allocations | Long-term history at huge scale without tuning | Recommended for `RateLimiting` + `Allocations` |
| [Lucene](lucene.md) | Full-text / field search on file-backed indexes | Operational complexity vs MongoDB | Single-host / PVC only |

### Mental model

1. **Counters and hot runtime state** → Redis (`RateLimiting`, often `Allocations`).
2. **Durable catalog and history** → MongoDB (`Configuration`, `Statistics`) or JsonFile/SQLite locally.
3. **File on a network mount** is still JsonFile or Lucene — not a separate “NFS provider”.
4. **Mixed roles** are normal: only override roles that benefit from a different backend.

## Suggested layouts

### Local development (default)

| Role | Provider | Notes |
| --- | --- | --- |
| All | `JsonFile` | `./data`; `seed_data.py`, `Seed` config, or `/api/v1/seed` |

The seed API exports **runtime catalog entities**, not raw collection file names on disk. See [Seed system](../core/seed-system.md).

Fastest path: clone, run API + Admin UI, seed catalog. No Docker required.

### Local development with SQLite statistics

| Role | Provider | Notes |
| --- | --- | --- |
| `Configuration` | `JsonFile` | `./data` |
| `Statistics` | `Sqlite` | `./data/statistics.db` — avoids loading entire `UsageSnapshots.json` |

See [SQLite](sqlite.md).

### Production — balanced (recommended)

| Role | Provider | Why |
| --- | --- | --- |
| `Configuration` | MongoDB | Shared, durable, queryable catalog |
| `Statistics` | MongoDB | Large snapshots; indexed queries |
| `RateLimiting` | Redis | Native atomic counters + TTL |
| `Allocations` | Redis | Fast acquire/release counters |

Use separate Redis `DatabaseIndex` (or key prefixes) per role on one server.

### Production — minimal ops

| Role | Provider |
| --- | --- |
| All | MongoDB |

Works for smaller deployments. Rate-limit paths are slightly less ideal than Redis but operationally simpler.

### Single host with shared volume (PVC / NFS)

| Role | Provider | Caveat |
| --- | --- | --- |
| `Configuration` | JsonFile on mounted path | One writer; not true HA |
| `Statistics` | JsonFile or SQLite on mount | SQLite: one writer; WAL enabled |
| `RateLimiting` / `Allocations` | Redis (still external) | Do not put hot counters on NFS |

The API **blocks** JsonFile, SQLite, and Lucene for `Statistics`, `RateLimiting`, and `Allocations` in non-Development environments — use MongoDB or Redis for those roles in production.

## Provider guides

| Guide | When to read |
| --- | --- |
| [JsonFile](json-file.md) | Default dev and seed data on disk |
| [SQLite](sqlite.md) | Embedded SQL document store for large local histories |
| [MongoDB](mongodb.md) | Production durable storage |
| [Redis](redis.md) | Rate limits, allocations, optional all-in-Redis |
| [Lucene](lucene.md) | Embedded search index on disk |

## Related reading

- [Configuration reference — Persistence](../configuration-reference.md#persistence)
- [Architecture — storage layering](../core/architecture.md)
- [Usage and observability](../core/usage-and-observability.md) — statistics role and snapshots
