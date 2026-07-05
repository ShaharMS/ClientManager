# seed_data.py

Seeds the public ClientManager API with demo catalog data and optionally writes historical usage snapshots to the JSON file store.

For **catalog-only** import/export (instance copy, appsettings generation), prefer the seed API — see [Seed system](../core/seed-system.md) (`GET` / `POST` / `PUT` `/api/v1/seed`).

## Prerequisites

- `ClientManager.Api` running (default `http://localhost:5062`)

## What it creates

| Resource | Count (default catalog) |
| --- | --- |
| Services | 20 |
| Resource pools | 10 |
| Client configurations | 25 |
| Global rate limits | Per service and pool |
| Usage snapshots | Optional multi-granularity history |

Catalog definitions live in [`configuration.py`](../../_scripts/configuration.py).

## Usage

```powershell
python _scripts/seed_data.py
```

### Common flags

| Flag | Default | Description |
| --- | --- | --- |
| `--base-url` | `http://localhost:5062` | Public API base URL |
| `--skip-history` | off | Catalog only; no `UsageSnapshots.json` write |
| `--history-days` | 395 | Days of historical usage to generate |
| `--history-seed` | 8675309 | RNG seed for deterministic history |
| `--history-data-dir` | `./data` or env | Where `UsageSnapshots.json` is written |
| `--replace-history` | off | Replace FiveMinute/Hour/Day snapshots instead of merging |

### Examples

```powershell
# Catalog + history (default)
python _scripts/seed_data.py --base-url http://localhost:5062

# Catalog only
python _scripts/seed_data.py --skip-history

# Regenerate history into a custom data directory
python _scripts/seed_data.py --history-data-dir ./data --replace-history --history-days 30
```

## Historical usage

When history is enabled, the script writes [`UsageSnapshots.json`](../../ClientManager.DataAccess/) directly under `--history-data-dir`. The API must be restarted after file-backed seeding unless using the JsonFile mtime reload (external writers are picked up on next read).

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Connection refused | Start `ClientManager.Api` first |
| HTTP 409 on seed | Entry already exists — script skips or reports `exists` |
| Charts empty after history seed | Restart API or wait for statistics cache TTL; confirm `--history-data-dir` matches API data directory |

## Related

- [Seed system](../core/seed-system.md) — runtime seed API and appsettings workflow
- [traffic_generator.py](traffic-generator.md) — live traffic on top of seeded data
