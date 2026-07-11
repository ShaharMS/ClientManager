# Docker Compose stacks

Stack definitions live in this folder. Use the repo-root [`docker-compose.yml`](../docker-compose.yml) as the single entry point for `docker compose up` — edit its `include` list to switch configurations.

| File | Purpose |
| --- | --- |
| [`default.yml`](default.yml) | Single API + Admin UI with JsonFile (`../data` volume) |
| [`dev.redis.yml`](dev.redis.yml) | Overlay: adds Redis and wires API `depends_on` |
| [`multipod.yml`](multipod.yml) | Three API replicas + MongoDB + Redis (shared persistence) |

## Commands

**Default (after `docker-compose.yml` includes `default.yml`):**

```bash
docker compose up --build
```

**With Redis** — set root `docker-compose.yml` to include `default.yml` + `dev.redis.yml`, then:

```bash
docker compose up --build
```

**Multi-pod** — set root `docker-compose.yml` to include only `multipod.yml`, then:

```bash
docker compose up --build -d
python _scripts/seed_data.py --base-url http://localhost:5062
python _scripts/statistics_multipod_check.py
docker compose down
```

Pods listen on host ports **5062**, **5063**, and **5064**.
