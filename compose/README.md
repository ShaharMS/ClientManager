# Docker Compose stacks

Stack definitions live in this folder. Use the repo-root [`docker-compose.yml`](../docker-compose.yml) for `docker compose up` (default: single API + Admin UI).

| File | Purpose |
| --- | --- |
| [`default.yml`](default.yml) | Single API + Admin UI |
| [`dev.redis.yml`](dev.redis.yml) | Overlay: adds Redis and wires API `depends_on` |
| [`redis.yml`](redis.yml) | Standalone Redis (`docker compose -f compose/redis.yml up -d`) |
| [`dev.mongo.yml`](dev.mongo.yml) | Standalone MongoDB replica set for integration tests |
| [`otel.yml`](otel.yml) | Jaeger all-in-one with OTLP on port 4317 |
| [`multipod.yml`](multipod.yml) | Three API replicas + MongoDB replica set + Redis (production-like persistence) |

Persistence uses **MongoDB** and **Redis** only. Storage roles: `Configuration`, `RateLimiting`, `Rpm`.

## Multi-pod verification (recommended)

Fresh cluster every run — empty Mongo/Redis volumes, catalog-only seed:

```bash
python _scripts/run_multipod_docker.py
```

This runs `docker compose down -v`, `up --build`, cross-pod checks, then tears down.

Options:

| Flag | Effect |
| --- | --- |
| `--keep-up` | Leave the stack running after the check |
| `--skip-check` | Only start the stack (no seed/check) |
| `--no-build` | Skip image rebuild |

For catalog migration between instances, use `GET` / `POST` `/api/v1/seed` with `Seed:SeedApiEnabled: true` — see [Seed system](../docs/core/seed-system.md).

Pods listen on host ports **5062**, **5063**, and **5064**.

## Multipod persistence layout

Typical `multipod.yml` environment:

| Role | Provider |
| --- | --- |
| `Configuration` (default) | MongoDB |
| `RateLimiting` | Redis (`DatabaseIndex: 1`) |
| `Rpm` | Redis (`DatabaseIndex: 2`) |
