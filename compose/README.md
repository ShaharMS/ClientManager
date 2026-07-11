# Docker Compose stacks

Stack definitions live in this folder. Use the repo-root [`docker-compose.yml`](../docker-compose.yml) for `docker compose up` (default: single API + Admin UI).

| File | Purpose |
| --- | --- |
| [`default.yml`](default.yml) | Single API + Admin UI with JsonFile (`../data` volume) |
| [`dev.redis.yml`](dev.redis.yml) | Overlay: adds Redis and wires API `depends_on` |
| [`multipod.yml`](multipod.yml) | Three API replicas + MongoDB + Redis (production-like persistence) |

## Multi-pod verification (recommended)

Fresh cluster every run — empty Mongo/Redis volumes, catalog-only seed, no historical usage:

```bash
python _scripts/run_multipod_docker.py
```

This runs `docker compose down -v`, `up --build`, `statistics_multipod_check.py` (which seeds via `seed_data.py --skip-history`), then tears down again.

Options:

| Flag | Effect |
| --- | --- |
| `--keep-up` | Leave the stack running after the check |
| `--skip-check` | Only start the stack (no seed/check) |
| `--no-build` | Skip image rebuild |

For richer seed data (including statistics history), import NDJSON manually via the [seed API](../docs/core/seed-system.md) after the stack is up.

**Disk:** the API image build excludes `data/` via [`.dockerignore`](../.dockerignore). Do not remove those exclusions — `UsageSnapshots.json` is ~1.5 GB and will bloat the build context.

Pods listen on host ports **5062**, **5063**, and **5064**.
