# Docker Compose stacks

Stack definitions live in this folder. Use the repo-root [`docker-compose.yml`](../docker-compose.yml) for `docker compose up` (default: multipod + observability + traffic-gen).

| File | Purpose |
| --- | --- |
| [`default.yml`](default.yml) | Single API + Admin UI |
| [`dev.redis.yml`](dev.redis.yml) | Overlay: adds Redis and wires API `depends_on` |
| [`redis.yml`](redis.yml) | Standalone Redis (`docker compose -f compose/redis.yml up -d`) |
| [`dev.mongo.yml`](dev.mongo.yml) | Standalone MongoDB replica set for integration tests |
| [`observability.yml`](observability.yml) | Prometheus + Grafana; `--profile traces` adds Tempo + OTel Collector |
| [`traffic-gen.yml`](traffic-gen.yml) | `--profile load` synthetic access-check traffic (multipod) |
| [`multipod.yml`](multipod.yml) | Three API replicas + MongoDB + Redis + Admin UI |
| [`otel.yml`](otel.yml) | Deprecated pointer — use `observability.yml --profile traces` |

Persistence uses **MongoDB** and **Redis** only. Storage roles: `Configuration`, `RateLimiting`, `Rpm`.

## Switching stacks

Edit `docker-compose.yml` `include:` list. Observability walkthroughs: [docs/observability/local.md](../docs/observability/local.md).

## Multi-pod verification

```bash
python _scripts/run_multipod_docker.py
```

| Flag | Effect |
| --- | --- |
| `--keep-up` | Leave the stack running after the check |
| `--skip-check` | Only start the stack (no seed/check) |
| `--no-build` | Skip image rebuild |

## Production monitoring

Import the dashboard and configure scrape targets — see [docs/observability/](../docs/observability/index.md).
