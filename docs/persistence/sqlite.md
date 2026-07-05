# SQLite provider



**Enum:** `PersistenceProvider.Sqlite`  

**Options:** `DefaultSqlite` / per-role `Sqlite` → `DatabasePath` (default `./data/statistics.db`)  

**Scope today:** **`Statistics` role only** — implemented as `SqliteUsageSnapshotDatabase` with a normalized bucket schema.



Catalog, rate limits, and allocations stay on JsonFile (or another provider) via `Roles` overrides.



## Good at



- **Fast range queries** on usage buckets — indexed `target_id`, `granularity`, `bucket_start`.

- **Low API memory** — no large `UsageSnapshots.json` in heap.

- **Single-machine prod** — one API instance with local disk.



## Weak at



- **Many concurrent writers** — SQLite is one writer; fine for statistics merge/rollup, not for fan-out rate-limit counters.

- **Multi-replica API** — each replica needs its own DB or you accept single-writer; not a shared cluster store like MongoDB.

- **Replacing all roles** — only statistics are implemented; do not set `DefaultProvider` to Sqlite for the whole app.



## Schema (statistics)



- `usage_snapshots` — segment metadata (client, target, granularity)

- `usage_buckets` — per-bucket grants/denials (aggregation target)

- `usage_counters` — pending usage counters before rollup



## Configuration



**Mixed layout** (recommended):



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



Environment variables:



```powershell

$env:Persistence__DefaultJsonFile__DataDirectory = "./data"

$env:Persistence__Roles__Statistics__Provider = "Sqlite"

$env:Persistence__Roles__Statistics__Sqlite__DatabasePath = "./data/statistics.db"

```



## See also



- [JsonFile](json-file.md)

- [MongoDB](mongodb.md) — production-scale shared statistics

- [Persistence overview](index.md)

