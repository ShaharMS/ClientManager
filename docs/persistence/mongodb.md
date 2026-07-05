# MongoDB provider

**Enum:** `PersistenceProvider.MongoDb`  
**Options:** `DefaultMongoDb` / per-role `MongoDb` → `ConnectionString`, `DatabaseName`, TLS, pool settings

Each logical collection maps to a MongoDB collection with the same name. Counters use a dedicated `_counters` collection. Queries and counts run server-side.

## Good at

- **Production multi-instance** — shared durable state across API replicas.
- **Configuration + statistics** — large documents, indexed filters, no full-file RAM load.
- **Operational familiarity** — backups, replication, Atlas/managed Mongo.
- **Default “everything durable”** — single `DefaultProvider: MongoDb` works for smaller prod deployments.

## Weak at

- **Ultra-hot counter paths** — works, but Redis is simpler for rate-limit and allocation counters at very high QPS.
- **Local zero-deps dev** — requires a running MongoDB (Docker Compose or Atlas).

## Storage role fit

| Role | MongoDB? |
| --- | --- |
| `Configuration` | Recommended prod |
| `Statistics` | Recommended prod |
| `RateLimiting` | Possible; Redis usually better |
| `Allocations` | Possible; Redis usually better |

## Configuration

**All roles on MongoDB:**

```json
{
  "Persistence": {
    "DefaultProvider": "MongoDb",
    "DefaultMongoDb": {
      "ConnectionString": "mongodb://mongo:27017",
      "DatabaseName": "ClientManager"
    }
  }
}
```

**Mixed with Redis** (common production layout):

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

```text
Persistence__DefaultProvider=MongoDb
Persistence__DefaultMongoDb__ConnectionString=mongodb://mongo:27017
```

## vs SQLite (statistics)

| | MongoDB | SQLite |
| --- | --- | --- |
| Shared replicas | Yes | No (single writer) |
| Local dev statistics | Heavier setup | One file, no server |
| Prod statistics | Yes | Single-node only |

## See also

- [Redis](redis.md)
- [Persistence overview — suggested layouts](index.md#suggested-layouts)
- [Configuration reference](../configuration-reference.md#persistence)
