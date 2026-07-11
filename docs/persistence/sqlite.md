# SQLite provider

**Enum:** `PersistenceProvider.Sqlite`  
**Options:** `DefaultSqlite` / per-role `Sqlite` → `DatabasePath` (default `./data/store.db`)

SQLite implements the same **document store** contract as JsonFile, MongoDB, Redis, and Lucene: logical collections, JSON documents, and counter keys in one database file. WAL mode is enabled; filtered search loads matching collection rows and evaluates queries in memory.

## Good at

- **Zero infrastructure** — one file on disk, no separate database server.
- **Large local histories** — `UsageSnapshots` stay on disk instead of loading a whole JSON file into RAM.
- **Single-host deployments** — durable catalog or statistics on a laptop, VM, or PVC.
- **Mixed layouts** — JsonFile for catalog, SQLite for `Statistics` (common dev upgrade path).

## Weak at

- **Multi-instance API replicas** — SQLite is single-writer; not a shared cluster store like MongoDB.
- **Very hot counters** — rate limits and allocations still want Redis at high QPS.
- **Complex ad-hoc queries** — no server-side field indexes yet; `SearchAsync` scans the collection in memory.

## Storage role fit

| Role | SQLite? |
| --- | --- |
| `Statistics` | **Strong fit** for single-host / dev with large snapshot history |
| `Configuration` | OK locally; MongoDB or JsonFile more typical |
| `RateLimiting` | OK locally; prefer Redis in prod |
| `Allocations` | OK locally; prefer Redis in prod |

## Configuration

**Mixed layout** (JsonFile catalog, SQLite statistics):

```json
{
  "Persistence": {
    "DefaultProvider": "JsonFile",
    "DefaultJsonFile": {
      "DataDirectory": "./data"
    },
    "Roles": {
      "Statistics": {
        "Provider": "Sqlite",
        "Sqlite": {
          "DatabasePath": "./data/statistics.db"
        }
      }
    }
  }
}
```

**All roles on SQLite:**

```json
{
  "Persistence": {
    "DefaultProvider": "Sqlite",
    "DefaultSqlite": {
      "DatabasePath": "./data/store.db"
    }
  }
}
```

```text
Persistence__Roles__Statistics__Provider=Sqlite
Persistence__Roles__Statistics__Sqlite__DatabasePath=./data/statistics.db
```

## Files on disk

| Path | Contents |
| --- | --- |
| Your `DatabasePath` | `documents` table (collection + id + json) and `counters` table |

Parent directories are created automatically on first use.

## NFS / shared volumes

Pointing `DatabasePath` at a network mount still uses **SQLite file semantics** (single writer, file locks). Treat as single-host or careful single-writer — same caveats as JsonFile on NFS.

The API blocks SQLite for `Statistics`, `RateLimiting`, and `Allocations` in non-Development environments, like JsonFile and Lucene. Use MongoDB or Redis for those roles in multi-instance production.

## See also

- [JsonFile](json-file.md) — simpler dev default
- [MongoDB](mongodb.md) — shared production documents and statistics
- [Persistence overview](index.md#suggested-layouts)
