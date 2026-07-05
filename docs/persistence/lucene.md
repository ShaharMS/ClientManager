# Lucene provider

**Enum:** `PersistenceProvider.Lucene`  
**Options:** `DefaultLucene` / per-role `Lucene` → `IndexDirectory` (default `./lucene-index`)

[Lucene.NET](https://lucenenet.apache.org/) embedded index on disk. Collections are indexed for field-level and full-text search without an external database server.

## Good at

- **Search-heavy catalog** — filter and text search over documents on a single host.
- **Air-gapped / PVC deployments** — index files on a persistent volume, no MongoDB dependency.
- **Read-mostly configuration** — when you need better query ergonomics than raw JsonFile scans.

## Weak at

- **Multi-writer clusters** — index directory is not a shared database; treat as single-host or careful single-writer.
- **Hot counters** — rate limits and allocations still want Redis semantics; Lucene is a poor fit for `RateLimiting`.
- **Large statistics time-series** — use MongoDB, SQLite, or JsonFile with awareness of scale limits.
- **Operational simplicity** — MongoDB often wins for the same “durable shared docs” slot with less index tuning.

## Storage role fit

| Role | Lucene? |
| --- | --- |
| `Configuration` | Possible on single host |
| `Statistics` | Rare; prefer MongoDB/SQLite |
| `RateLimiting` | Not recommended |
| `Allocations` | Not recommended |

## Configuration

```json
{
  "Persistence": {
    "DefaultProvider": "Lucene",
    "DefaultLucene": {
      "IndexDirectory": "./lucene-index"
    }
  }
}
```

## NFS / shared volumes

Like JsonFile, `IndexDirectory` can point at a mounted path. You still get **Lucene index semantics** (single-writer, file locks), not a network database.

The API treats Lucene like JsonFile for **production policy** — non-Development deployments should not use Lucene for `Statistics`, `RateLimiting`, or `Allocations`.

## See also

- [JsonFile](json-file.md) — simpler file-backed dev default
- [MongoDB](mongodb.md) — shared production documents
- [Persistence overview](index.md)
