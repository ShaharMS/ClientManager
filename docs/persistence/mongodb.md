# MongoDB provider

**Enum:** `PersistenceProvider.MongoDb`  
**Options:** `DefaultMongoDb` / per-role `MongoDb` → `ConnectionString`, `DatabaseName`, TLS, pool settings

Each logical collection maps to a MongoDB collection with the same name. Counters use a dedicated `_counters` collection. Queries and counts run server-side.

## Good at

- **Production multi-instance** — shared durable state across API replicas.
- **Configuration catalogs** — durable documents and server-side queries.
- **Operational familiarity** — backups, replication, Atlas/managed Mongo.
- **Default “everything durable”** — single `DefaultProvider: MongoDb` works for smaller prod deployments.

## Weak at

- **Ultra-hot counter paths** — works, but Redis is simpler for rate-limit and RPM counters at very high QPS.
- **Local zero-deps dev** — requires a running MongoDB (Docker Compose or Atlas).

## Storage role fit

| Role | MongoDB? |
| --- | --- |
| `Configuration` | Recommended prod |
| `RateLimiting` | Possible; Redis usually better |
| `Rpm` | Possible; Redis usually better |

## Configuration

**All roles on MongoDB:**

```json
{
  "Persistence": {
    "DefaultProvider": "MongoDb",
    "DefaultMongoDb": {
      "ConnectionString": "mongodb://mongo:27017/?replicaSet=rs0",
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
      "ConnectionString": "mongodb://mongo:27017/?replicaSet=rs0",
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

```text
Persistence__DefaultProvider=MongoDb
Persistence__DefaultMongoDb__ConnectionString=mongodb://mongo:27017/?replicaSet=rs0
```

MongoDB token buckets use transactions. Configure a replica set whenever the
`RateLimiting` role uses MongoDB; `compose/dev.mongo.yml` provides a local
single-member replica set.

## See also

- [Redis](redis.md)
- [Persistence overview — suggested layouts](index.md#suggested-layouts)
- [Configuration reference](../configuration-reference.md#persistence)
