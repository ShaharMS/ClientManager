# Development and operations

Day-to-day notes for running, observing, deploying, and debugging ClientManager.

## Security model

**There is no built-in authentication or authorization** on the API or Admin UI today.

| Surface | Risk if exposed | Mitigation |
| --- | --- | --- |
| API catalog CRUD | Anyone can change clients and limits | Network policy, private VPC, reverse-proxy auth |
| Runtime gatekeeping | Anyone who can reach the API can check access | Same — usually internal-only |
| Admin UI | Full operational control | Do not expose publicly without a front door |
| Swagger `/docs` | Schema and try-it-out access | Disable or protect in production |
| Seed API | Bulk catalog wipe/import when `SeedApiEnabled` is true | Keep `Seed:SeedApiEnabled: false` in production |

ClientManager identifies **callers** via `clientId` you supply — it does not validate end-user identity.

## Danger zone {#danger-zone}

Settings and APIs that can **wipe or reshape production data** or change hot-path behavior. Opt in deliberately.

| Gate | Risk | Production default |
| --- | --- | --- |
| `Seed:SeedApiEnabled` | `POST`/`PUT`/`DELETE` `/api/v2/seed` can replace or clear the catalog | `false` |
| `DELETE /api/v2/seed` | Removes catalog documents | Disabled when seed API is off |
| `StorageReadCache:CatalogTtl` / `HotPathCatalogTtl` | Lower TTL = more storage reads; very high TTL = stale limits after Admin UI edits | See [Configuration reference](configuration-reference.md#storagereadcache) |
| `docker compose down -v` | Destroys MongoDB/Redis volumes in multipod compose | Dev/test only |

Details: [Seed system](core/seed-system.md), [Configuration reference](configuration-reference.md).

## Local development tips

### Startup order

1. `ClientManager.Api` (`:5062`) — requires Redis (default) or configured MongoDB
2. `ClientManager.AdminUI` (`:5100`)

### Seed catalog data

```powershell
python _scripts/seed_data.py --base-url http://localhost:5062
```

Or enable `Seed:SeedApiEnabled: true` and use `GET` / `POST` `/api/v2/seed` — see [Seed system](core/seed-system.md).

### Hot-path cache behavior

Configuration reads on the access-check path use `IStorageReadCache` (`StorageReadCache:CatalogTtl`, default 30s). Admin UI writes invalidate cache entries — no API restart needed.

## Python scripts (`_scripts/`)

Full documentation: **[Scripts](scripts/index.md)**.

| Script | Purpose |
| --- | --- |
| `seed_data.py` | POST demo catalog to the API |
| `traffic_generator.py` | Continuous random access checks |
| `performance_baseline.py` | Deterministic load profile + latency report |
| `launch_observability_ui.py` | Local Prometheus + Grafana (`--traces` for Tempo) |
| `download_images.py` | Pull/build Docker images |

All API-facing scripts accept `--base-url` (default `http://localhost:5062`).

### Observability stack

```powershell
python _scripts/launch_observability_ui.py up
```

Default UIs: Grafana http://localhost:3000/d/clientmanager-observability, Prometheus http://localhost:9090. Add `--traces` for Tempo on port 3200.

See [Observability guides](observability/index.md) for scrape config. Example Grafana RPM query:

```promql
sum(rate(clientmanager_requests_total[5m])) * 60
```

## Validation

| What | How |
| --- | --- |
| Solution build | `dotnet build ClientManager.slnx` |
| Manual API checks | Swagger at `/docs`, or curl against `/api/v2/access/check` |
| Dashboard validation | `seed_data.py` + `traffic_generator.py` |

## Docker and Compose

Repo-root `docker-compose.yml` is the entry point for `docker compose up`. Stack definitions live in `compose/` (see `compose/README.md` in the repository).

**Multi-pod verification** (MongoDB + Redis + three API replicas):

```bash
python _scripts/run_multipod_docker.py
```

Production-oriented deployments should:

- Set `Persistence` to MongoDB and/or Redis (see [Persistence overview](persistence/index.md))
- Mount secrets via environment variables
- Place API and Admin UI behind TLS termination
- Keep `Seed:SeedApiEnabled: false` unless migrating catalog

Dockerfiles:

| File | Image |
| --- | --- |
| `ClientManager.Api/Dockerfile` | `clientmanager/api:local` |
| `ClientManager.AdminUI/Dockerfile` | `clientmanager/adminui:local` |

## Logging

Both hosts use **NLog** (`nlog.config`). Every error body includes `traceId` (also in `X-Trace-Id`) — match it to NLog request logs and OpenTelemetry spans (`storage.access.check`, …).

## Multi-instance notes

| Concern | Guidance |
| --- | --- |
| Shared rate-limit state | `RateLimiting` and `Rpm` roles must use shared Redis or MongoDB |
| Configuration | MongoDB recommended for durable catalog |
| Cache | Each API instance has its own in-memory `StorageReadCache`; catalog reads may be stale on other pods until `CatalogTtl` expires |
| RPM | `RpmAccountingService` buffers per replica; flushes to shared `Rpm` role storage |

## Troubleshooting

### Empty dashboard / no clients

- Storage empty or API cannot reach Redis/MongoDB.
- Fix: seed via script or seed API; confirm persistence connection strings.

### `503 Service Unavailable`

- Storage backend unreachable. Check API startup logs.

### Client gets `429` but limit looks fine in the UI

- Check **global** rate limit for the service (`GlobalRateLimit` with `id` = `serviceId`).
- Check client `exemptFromGlobalLimits` / `contributesToGlobalLimits` flags.
- `GET /access/check` **consumes** quota — do not use it as a monitoring poll.

### `401` vs `403`

- `401` — no `services[serviceId]` entry (not configured).
- `403` — client disabled, service disabled, or `isAllowed: false`.

### Slow access checks (>250 ms logged)

- Review persistence provider choice and hot-path metrics at `/prometheus/otel`.

### Admin UI cannot connect

- API not running, or `ApiBaseUrl` mismatch.
- Docker: Admin UI must use `http://api:5062`, not `localhost`.

### Seed endpoints return 404

- Set `Seed:SeedApiEnabled: true` (Development default in `appsettings.Development.json`).

## License

The project is licensed under **GNU GPL v3** (`LICENSE`).

## Related reading

- [Configuration reference](configuration-reference.md)
- [Persistence overview](persistence/index.md)
- [Getting started](getting-started.md)
- [Usage and observability](core/usage-and-observability.md)
