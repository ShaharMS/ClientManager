# Storage migration guide

Move catalog and statistics from one persistence backend (provider X) to an empty target (provider Y) using two running API instances and the seed API.

**Long-running operations:** export, import, and delete paginate internally but can take minutes to hours for large statistics volumes. Only one seed operation may run at a time. Do not start another seed request until the current one finishes.

## Prerequisites

- **Instance A** — connected to provider X (source data)
- **Instance B** — connected to provider Y (**empty** statistics and catalog for collections you will POST)
- Network access from your workstation to both APIs

## Response formats

| Request | Response |
| --- | --- |
| Catalog-only `GET /seed` (no `usageSnapshots`) | JSON — paste into appsettings `Seed` |
| `GET /seed?include=...,usageSnapshots` or `format=ndjson` | Download `seed.ndjson` (streamed NDJSON) |
| `POST` / `PUT` with `Content-Type: application/x-ndjson` | NDJSON progress stream (`_progress` + `_summary` lines) |
| `POST` / `PUT` with JSON body | JSON `SeedImportSummary` |

NDJSON lines use a `$type` field (`service`, `clientConfiguration`, `usageSnapshot`, …). Lines with `$type` of `_progress` or `_summary` are control records; importers ignore them (safe when re-uploading an export file).

## DELETE → POST vs PUT

| Goal | Steps |
| --- | --- |
| **Full replace** on empty target | `POST` (requires empty included collections) |
| **Wipe then replace** | `DELETE` then `POST` |
| **Merge / concat** into existing data | `PUT ?strategy=skip` (or `replace` to upsert by ID) |

`POST` returns HTTP 409 when any included collection is not empty. The error message names the collection and points to `DELETE` or `PUT`.

## Full migration (catalog + statistics)

Replace `http://instance-a:5062` and `http://instance-b:5062` with your base URLs.

### 1. Export from Instance A

```bash
curl -fS --no-progress-meter \
  -OJ "http://instance-a:5062/api/v1/seed?include=services,resourcePools,globalRateLimits,clientConfigurations,usageSnapshots"
```

This writes `seed.ndjson` in the current directory. The server streams progress lines into the file as it exports.

### 2. Wipe target collections on Instance B (if needed)

Skip when Instance B is already empty for every collection in your import.

```bash
curl -fS --no-progress-meter -N -X DELETE \
  "http://instance-b:5062/api/v1/seed?include=services,resourcePools,globalRateLimits,clientConfigurations,usageSnapshots"
```

Catalog-only delete returns JSON counts. Requests that include `usageSnapshots` return an NDJSON progress stream.

### 3. Import into Instance B

```bash
curl -fS --no-progress-meter -N -X POST \
  "http://instance-b:5062/api/v1/seed?include=services,resourcePools,globalRateLimits,clientConfigurations,usageSnapshots" \
  -H "Content-Type: application/x-ndjson" \
  --data-binary @seed.ndjson
```

Read the response stream for `_progress` and the final `_summary` line.

## Statistics-only migration

When Instance B already has catalog data (for example from appsettings `Seed`):

```bash
# Export statistics from A
curl -fS --no-progress-meter -OJ \
  "http://instance-a:5062/api/v1/seed?include=usageSnapshots"

# Wipe statistics on B if needed
curl -fS --no-progress-meter -N -X DELETE \
  "http://instance-b:5062/api/v1/seed?include=usageSnapshots"

# Import statistics to B
curl -fS --no-progress-meter -N -X POST \
  "http://instance-b:5062/api/v1/seed?include=usageSnapshots" \
  -H "Content-Type: application/x-ndjson" \
  --data-binary @seed.ndjson
```

## Merge statistics without wiping

```bash
curl -fS --no-progress-meter -N -X PUT \
  "http://instance-b:5062/api/v1/seed?include=usageSnapshots&strategy=skip" \
  -H "Content-Type: application/x-ndjson" \
  --data-binary @seed.ndjson
```

`strategy=skip` creates snapshots whose IDs are not already present. Use `strategy=replace` to upsert by ID.

## Catalog-only (appsettings workflow)

Unchanged — JSON export for paste into configuration:

```bash
curl -fS "http://instance-a:5062/api/v1/seed" | jq .
```

To download catalog as NDJSON instead:

```bash
curl -fS -OJ "http://instance-a:5062/api/v1/seed?format=ndjson"
```

## Provider notes

| Provider | Notes |
| --- | --- |
| **Redis** | Export/import uses paginated key reads and batched writes; viable at large scale. |
| **MongoDB** | Preferred statistics backend for very large histories. |
| **JsonFile** | Fine for dev; entire file loads into memory — avoid multi-GB statistics on JsonFile. |
| **SQLite** | Embedded document store; lower memory than JsonFile for large `UsageSnapshots`; same seed API. |

Pending atomic usage counters are **not** exported — they are ephemeral and folded into snapshots on flush.

## Related

- [Seed system](../core/seed-system.md)
- [Persistence overview](../persistence/index.md)
