# JsonFile provider

**Enum:** `PersistenceProvider.JsonFile`  
**Options:** `DefaultJsonFile` / per-role `JsonFile` → `DataDirectory` (default `./data`)

Each logical collection is one JSON file (e.g. `ClientConfiguration.json`, `UsageSnapshots.json`). Counters live in `_counters.json`. The store keeps an in-memory cache and writes through to disk on change.

## Good at

- **Zero infrastructure** — clone the repo and run.
- **Inspectable data** — edit or diff JSON files directly.
- **Seed workflows** — `seed_data.py` writes here by design.
- **Small and medium datasets** — catalog CRUD, modest statistics history.

## Weak at

- **Large `UsageSnapshots.json`** — entire file loaded into API RAM; `SearchAsync` scans in memory.
- **Multi-instance writes** — no distributed locking; mtime reload can spike memory when external writers touch files.

## Storage role fit

| Role | JsonFile? |
| --- | --- |
| `Configuration` | Excellent for dev |
| `RateLimiting` | OK locally; prefer Redis in prod |
| `Allocations` | OK locally; prefer Redis in prod |
| `Statistics` | OK for dev/small; use MongoDB for large histories or prod |

## Configuration

```json
{
  "Persistence": {
    "DefaultProvider": "JsonFile",
    "DefaultJsonFile": {
      "DataDirectory": "./data"
    }
  }
}
```

```text
Persistence__DefaultJsonFile__DataDirectory=./data
```

## Files on disk

| File | Contents |
| --- | --- |
| `ClientConfiguration.json` | Clients |
| `services.json` | Services |
| `resource_pools.json` | Pools |
| `GlobalRateLimit.json` | Global limits |
| `ResourceAllocation.json` | Allocations |
| `UsageSnapshots.json` | Usage history |
| `_counters.json` | Rate-limit and allocation counters |

## NFS / shared volumes

Pointing `DataDirectory` at a network mount (NFS, PVC) still uses **JsonFile semantics**. You get shared files, not a distributed database. Use for single-writer or dev-only topologies.

## See also

- [Persistence overview](index.md)
