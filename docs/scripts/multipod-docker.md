# Multi-pod Docker verification

Run three API replicas with **MongoDB + Redis** (shared persistence) in Docker. Each run starts with **empty volumes**; catalog is seeded automatically; no historical usage unless you import NDJSON manually.

## One command

```bash
python _scripts/run_multipod_docker.py
```

What it does:

1. `docker compose -f compose/multipod.yml down -v` — wipe previous Mongo/Redis data
2. `docker compose up --build -d` — build one API image, start 3 replicas + infra
3. Wait until pods on **5062**, **5063**, **5064** respond
4. `statistics_multipod_check.py` — seeds catalog (`seed_data.py --skip-history`), generates traffic, checks cross-pod totals and latency
5. `docker compose down -v` — tear down (skip with `--keep-up`)

## Manual steps

```bash
docker compose -f compose/multipod.yml down -v
docker compose -f compose/multipod.yml up --build -d
python _scripts/statistics_multipod_check.py
docker compose -f compose/multipod.yml down -v
```

## Seeding richer data

The automated path seeds **catalog only** (clients, services, pools, limits). For statistics history or full instance copies, use the seed API with NDJSON after the stack is up — see [Seed system](../core/seed-system.md) and [Storage migration](../migration/storage-migration.md).

## Disk hygiene

| Item | Notes |
| --- | --- |
| Docker build context | Root [`.dockerignore`](../.dockerignore) excludes `data/` (~1.5 GB `UsageSnapshots.json`) |
| Docker prune | `docker system prune -a --volumes` and `docker builder prune -a` when images pile up |
| Repo `data/` | JsonFile dev artifacts; safe to delete if you use Mongo/Redis or re-seed |
| `act-*` volumes | Leftover from local GitHub Actions (`act`); safe to remove |

## Related scripts

| Script | Purpose |
| --- | --- |
| [`run_multipod_docker.py`](../../_scripts/run_multipod_docker.py) | Full fresh multipod cycle |
| [`statistics_multipod_check.py`](../../_scripts/statistics_multipod_check.py) | Cross-pod totals + latency budget |
| [`statistics_multipod_stress.py`](../../_scripts/statistics_multipod_stress.py) | Heavier split vs single-writer traffic |

See also [`compose/README.md`](../../compose/README.md).
