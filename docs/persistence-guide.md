# Persistence Guide

## What This Guide Explains

This project supports multiple persistence providers, but it does **not** choose a backend independently for every entity or request. Instead, `ClientManager.Api` assigns one backend to each logical **storage role** at startup, and every repository in that role uses the same configured provider.

If you are trying to understand what happens when you provide Redis, MongoDB, or an NFS-backed shared volume, this is the split that matters.

## The Actual Storage Owner

The persistence owner is `ClientManager.Api`.

- `ClientManager.AdminUI` talks to `ClientManager.Api`
- `ClientManager.Api` references `ClientManager.DataAccess` in-process via `AddInProcessStorageServices`

That means provider configuration is resolved when the API host starts, not in the Admin UI.

## Storage Roles

Persistence is divided into four logical roles:

| Role | What it stores |
| --- | --- |
| `Configuration` | Client configurations, services, resource pools, and global rate limits |
| `RateLimiting` | Runtime rate-limit counters |
| `Allocations` | Resource allocation documents and allocation counters |
| `Statistics` | Usage snapshot time-series documents |

This is the routing boundary. If a role is mapped to Redis, everything in that role uses Redis. If a role is mapped to MongoDB, everything in that role uses MongoDB.

## What Each Role Maps To

The code currently routes data like this:

| Role | Concrete collections / data |
| --- | --- |
| `Configuration` | `ClientConfiguration`, `services`, `resource_pools`, `GlobalRateLimit` |
| `RateLimiting` | Counter keys only |
| `Allocations` | `ResourceAllocation` plus `alloc-count:*` counters |
| `Statistics` | `UsageSnapshots` |

## How Default And Per-Role Binding Works

The `Persistence` section supports two configuration layers:

- `DefaultProvider` and its matching `Default*` settings apply to every role that does not have its own override.
- `Roles` overrides let you bind specific roles to a different provider.

So the model is:

- default provider for the whole app
- optional per-role exceptions

It is not:

- one provider per entity
- one provider per request
- automatic backend balancing

## What Redis Actually Does

Redis is used as a document store plus a counter store.

- Collections are stored under Redis keys grouped by collection name.
- Rate-limit state uses Redis counters with expiry windows.
- Allocation counters also use Redis counters.
- If RediSearch is available, search/count operations can use native Redis search support.
- If RediSearch is not available, document filtering falls back to in-memory evaluation after reading from Redis.

In practical terms, Redis can hold both durable-ish documents and ephemeral runtime counters in this project, but the hot-path benefit is strongest for `RateLimiting` and `Allocations` because those roles rely heavily on atomic counters.

## What MongoDB Actually Does

MongoDB is the document-oriented durable backend.

- Each logical collection maps to a MongoDB collection with the same name.
- Counters are stored in a dedicated `_counters` collection.
- Search/count operations can be translated to MongoDB queries instead of always loading everything into memory.

MongoDB is the most natural fit when you want durable shared state across multiple app instances.

## What NFS Actually Means Here

NFS is **not** a first-class provider in the codebase.

When people say “use NFS” in this project, what that really means is one of these file-backed providers writes to a directory that happens to be mounted from a network file share:

- `JsonFile`
- `Lucene`

So if you point `DataDirectory` or `IndexDirectory` at an NFS mount, the app does not behave like a new “NFS database provider”. It still behaves like a JSON-file store or a Lucene index store, only the files live on a shared volume.

That distinction matters because file-backed providers are documented in this repository as local or single-host oriented backends, not the recommended topology for multi-instance production deployment.

## If Everything Uses Redis

If you configure only a default Redis provider, then all four roles use Redis.

Example:

```json
{
  "Persistence": {
    "DefaultProvider": "Redis",
    "DefaultRedis": {
      "ConnectionString": "redis:6379",
      "DatabaseIndex": 0,
      "UseTls": false
    }
  }
}
```

What happens:

- `Configuration` data goes to Redis
- `RateLimiting` counters go to Redis
- `Allocations` documents and counters go to Redis
- `Statistics` snapshots go to Redis

The data is still separated logically by role, collection name, and counter key pattern. It is not split into different application subsystems automatically unless you add per-role bindings.

## If Allocations And Rate Limiting Use Redis, And The Rest Use MongoDB

This is a common mixed topology.

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
        "Redis": {
          "ConnectionString": "redis:6379",
          "DatabaseIndex": 1
        }
      },
      "Allocations": {
        "Provider": "Redis",
        "Redis": {
          "ConnectionString": "redis:6379",
          "DatabaseIndex": 2
        }
      }
    }
  }
}
```

What happens:

- `Configuration` stays in MongoDB
- `Statistics` stays in MongoDB
- `RateLimiting` uses Redis counters
- `Allocations` uses Redis for both allocation documents and allocation counters

This keeps the hot runtime state on Redis while leaving long-lived configuration and history on MongoDB.

Using different Redis `DatabaseIndex` values is a clean way to separate Redis-backed roles while still reusing the same Redis server.

## If Allocations And Rate Limiting Use Redis, And The Rest Use NFS / Shared JSON Files

This is similar to the MongoDB split, but the non-Redis roles use `JsonFile` on a shared mount.

```json
{
  "Persistence": {
    "DefaultProvider": "JsonFile",
    "DefaultJsonFile": {
      "DataDirectory": "/mnt/nfs/clientmanager/data"
    },
    "Roles": {
      "RateLimiting": {
        "Provider": "Redis",
        "Redis": {
          "ConnectionString": "redis:6379",
          "DatabaseIndex": 1
        }
      },
      "Allocations": {
        "Provider": "Redis",
        "Redis": {
          "ConnectionString": "redis:6379",
          "DatabaseIndex": 2
        }
      }
    }
  }
}
```

What happens:

- `Configuration` uses JSON files on the mounted directory
- `Statistics` uses JSON files on the mounted directory
- `RateLimiting` uses Redis
- `Allocations` uses Redis

This is still fundamentally a file-backed setup for the non-Redis roles.

## Important Caveats

### File-backed providers are not the default production topology

The repository explicitly treats `JsonFile` and `Lucene` as local or single-host oriented backends. A shared volume may make data visible across hosts, but it does not turn those providers into a full distributed database design.

### Redis role separation can use different logical databases

Redis-backed roles can share the same Redis server and still be separated by using different `DatabaseIndex` values. Each `RedisDocumentStore` now binds to its configured logical database index directly, so this configuration works as expected:

- same Redis connection string and same `DatabaseIndex`: shared Redis database with logical key separation
- same Redis connection string and different `DatabaseIndex` values: same Redis server, separate logical Redis databases per role
- different Redis connection strings per role: separate Redis deployments or endpoints per role

### MongoDB database names are cleaner to split per role

MongoDB clients are cached by connection string, but the code resolves the target Mongo database name per store creation. That makes same-server, different-database-name layouts a cleaner fit than same-server, different-Redis-database-index layouts.

## Recommended Mental Model

Think about persistence in this project like this:

1. Pick a default provider for the whole system.
2. Override only the roles that benefit from a different backend.
3. Treat Redis primarily as runtime state and counter infrastructure.
4. Treat MongoDB as the durable shared document backend.
5. Treat NFS as a mounted location for file-backed providers, not as a provider of its own.

## Related Reading

- [Integration guide](integration-guide.md) — wire ClientManager in front of your services
- Repository `README.md`
- `ClientManager.DataAccess/README.md`