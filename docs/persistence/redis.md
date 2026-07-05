# Redis provider

**Enum:** `PersistenceProvider.Redis`  
**Options:** `DefaultRedis` / per-role `Redis` → `Host`, `Port`, `DatabaseIndex`, TLS, `GlobalKeyPrefix`, timeouts

Redis acts as both a **document store** (collections grouped by key naming) and a **counter store** (atomic INCR with TTL). With RediSearch, search/count can use native indexes; otherwise filtering falls back to in-memory evaluation after fetch.

## Good at

- **`RateLimiting`** — atomic counters, expiry windows, very high write rates.
- **`Allocations`** — allocation documents plus `alloc-count:*` counters on the hot acquire/release path.
- **Low-latency runtime state** — shared across API instances when all point at the same Redis.
- **Role isolation** — same Redis server, different `DatabaseIndex` per role.

## Weak at

- **Huge long-term statistics** — can store snapshots, but large historical analytics are usually better on MongoDB or SQLite with proper indexing.
- **Complex ad-hoc queries** — not a replacement for MongoDB on catalog search/reporting.
- **Durability expectations** — tune persistence (AOF/RDB) explicitly; rate-limit state is often ephemeral by design.

## Storage role fit

| Role | Redis? |
| --- | --- |
| `RateLimiting` | **Best fit** |
| `Allocations` | **Strong fit** |
| `Configuration` | Possible; MongoDB/JsonFile more typical |
| `Statistics` | Possible; not the default recommendation |

## Configuration

**All roles on Redis:**

```json
{
  "Persistence": {
    "DefaultProvider": "Redis",
    "DefaultRedis": {
      "Host": "redis",
      "Port": 6379,
      "DatabaseIndex": 0
    }
  }
}
```

**Split indexes on one server:**

```json
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
```

`Host` must be hostname only — put the port in `Port`, not `host:6379` in `Host`.

```text
Persistence__Roles__RateLimiting__Provider=Redis
Persistence__Roles__RateLimiting__Redis__Host=redis
Persistence__Roles__RateLimiting__Redis__DatabaseIndex=1
```

## Mental model

- Documents and counters share Redis but use different key patterns.
- Logical separation: collection name in key structure, `GlobalKeyPrefix`, or `DatabaseIndex`.
- `AbortOnConnectFail: false` lets the API start when Redis is temporarily down (dev); production should monitor connectivity.

## See also

- [MongoDB](mongodb.md) — durable catalog and statistics
- [Persistence overview](index.md#suggested-layouts)
