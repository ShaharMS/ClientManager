# Docker Compose stacks

Stack definitions live in this folder. Use the repo-root [`docker-compose.yml`](../docker-compose.yml) for `docker compose up` (default: single API + Admin UI).

| File | Purpose |
| --- | --- |
| [`default.yml`](default.yml) | Single API + Admin UI |
| [`dev.redis.yml`](dev.redis.yml) | Overlay: adds Redis and wires API `depends_on` |
| [`redis.yml`](redis.yml) | Standalone Redis (`docker compose -f compose/redis.yml up -d`) |
| [`dev.mongo.yml`](dev.mongo.yml) | Standalone MongoDB replica set for integration tests |
| [`observability.yml`](observability.yml) | Prometheus + Grafana; `--profile traces` adds Tempo + OTel Collector |
| [`traffic-gen.yml`](traffic-gen.yml) | `--profile load` synthetic access-check traffic (multipod) |
| [`multipod.yml`](multipod.yml) | Three API replicas + MongoDB replica set + Redis |
| [`otel.yml`](otel.yml) | Deprecated pointer — use `observability.yml --profile traces` |

Persistence uses **MongoDB** and **Redis** only. Storage roles: `Configuration`, `RateLimiting`, `Rpm`.

## Observability (local)

```powershell
# Metrics only (2 containers)
python _scripts/launch_observability_ui.py up

# With traces
python _scripts/launch_observability_ui.py up --traces
```

Full runbook: [docs/observability-runbook.md](../docs/observability-runbook.md)

## Multipod + dashboard

Edit `docker-compose.yml`:

```yaml
include:
  - path: compose/multipod.yml
  - path: compose/observability.yml
  - path: compose/traffic-gen.yml   # optional
```

```powershell
docker compose up --build
docker compose --profile load up    # skewed demo traffic
```

Pods: host ports **5062**, **5063**, **5064**. Prometheus scrapes each pod on the Docker network.

## Multi-pod verification (recommended)

```bash
python _scripts/run_multipod_docker.py
```

| Flag | Effect |
| --- | --- |
| `--keep-up` | Leave the stack running after the check |
| `--skip-check` | Only start the stack (no seed/check) |
| `--no-build` | Skip image rebuild |

## Production

Import [`observability/grafana/dashboards/clientmanager.json`](../observability/grafana/dashboards/clientmanager.json) into org Grafana. See [metrics catalog](../docs/metrics-catalog.md).
