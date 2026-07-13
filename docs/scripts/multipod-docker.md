# Multi-pod Docker verification

Run three API replicas with **MongoDB + Redis** (shared persistence) in Docker. Each run starts with empty volumes and uses the JSON seed API to create the catalog.

## One command

```bash
python _scripts/run_multipod_docker.py
```

What it does:

1. `docker compose -f compose/multipod.yml down -v` — wipe previous Mongo/Redis data
2. `docker compose up --build -d` — build one API image, start 3 replicas + infra
3. Wait until pods on **5062**, **5063**, **5064** respond
4. Catalog seed + `multipod_overview_check.py` against all replicas
5. `docker compose down -v` — tear down (skip with `--keep-up`)

## Manual steps

```bash
docker compose -f compose/multipod.yml down -v
docker compose -f compose/multipod.yml up --build -d
python _scripts/multipod_overview_check.py
docker compose -f compose/multipod.yml down -v
```

## Catalog seed data

The seed endpoint imports only clients, services, and global rate limits. It accepts JSON with `skip` or `replace` semantics; see [Seed system](../core/seed-system.md).

## Disk hygiene

| Item | Notes |
| --- | --- |
| Docker build context | Root [`.dockerignore`](../.dockerignore) excludes local build output |
| Docker prune | `docker system prune -a --volumes` and `docker builder prune -a` when images pile up |
| `act-*` volumes | Leftover from local GitHub Actions (`act`); safe to remove |

## Related scripts

| Script | Purpose |
| --- | --- |
| [`run_multipod_docker.py`](../../_scripts/run_multipod_docker.py) | Full fresh multipod cycle |

See also [`compose/README.md`](../../compose/README.md).
