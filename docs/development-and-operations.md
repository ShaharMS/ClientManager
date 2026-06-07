# Development and operations

Day-to-day notes for running, observing, deploying, and debugging ClientManager.

## Security model

**There is no built-in authentication or authorization** on the API or Admin UI today. `UseAuthorization()` is registered but no authentication scheme is configured.

Implications:

| Surface | Risk if exposed | Mitigation |
| --- | --- | --- |
| API catalog CRUD | Anyone can change clients, limits, pools | Network policy, private VPC, reverse-proxy auth |
| Runtime gatekeeping | Anyone who can reach the API can check/acquire | Same — usually internal-only |
| Admin UI | Full operational control | Do not expose publicly without a front door |
| Swagger `/docs` | Schema and try-it-out access | Disable or protect in production |

ClientManager identifies **callers** via `clientId` you supply — it does not validate end-user identity. Production integrations should derive `clientId` from trusted headers, API keys, or JWT claims at your edge layer, not from caller-editable query strings.

## Local development tips

### Startup order

1. `ClientManager.Api` (`:5062`)
2. `ClientManager.AdminUI` (`:5100`)

### Persistence location

With default JsonFile settings, data files land in `./data` relative to the API process working directory. Docker Compose mounts repo `./data` → `/app/data`.

If catalogs look empty after restart, run:

```powershell
python _scripts/seed_data.py --base-url http://localhost:5062
```

Or configure the `Seed` section — see [Configuration reference](configuration-reference.md).

### Hot-path cache behavior

Configuration reads on the access-check path use `IStorageReadCache` (default catalog TTL 30 seconds). After editing config in the Admin UI, changes propagate within TTL or immediately on write (cache invalidation). You do **not** need to restart the API for catalog edits.

## Python scripts (`_scripts/`)

| Script | Purpose |
| --- | --- |
| `seed_data.py` | POST demo clients, services, pools, and global limits to the catalog API |
| `traffic_generator.py` | Continuous random access checks and acquisitions for dashboard demos |
| `download_images.py` | Pull dependency images and/or build project Docker images |
| `launch_observability_ui.py` | Start local Grafana, Prometheus, and Jaeger; wire OTLP |
| `performance_baseline.py` | Deterministic load profile + latency report for before/after comparisons |
| `configuration.py` | Shared catalogs, URLs, and paths used by the scripts above |

All scripts accept `--base-url` where relevant (default `http://localhost:5062`).

### Observability stack

```powershell
python _scripts/launch_observability_ui.py
```

Default UIs: Grafana http://localhost:3000, Prometheus http://localhost:9090, Jaeger http://localhost:16686.

For scrape targets, metric catalogs, PromQL examples, production wiring, and security notes, see the [Metrics integration guide](metrics-integration-guide.md).

### Performance baseline

```powershell
python _scripts/performance_baseline.py --base-url http://localhost:5062
```

Reports latency percentiles and JsonFile storage sizes under `data/` for regression comparisons.

## Validation

| What | How |
| --- | --- |
| Solution build | `dotnet build ClientManager.slnx` |
| Manual API checks | Swagger at `/docs`, or curl against gatekeeping endpoints |
| Dashboard validation | `seed_data.py` + `traffic_generator.py` |

There is no automated test suite or CI workflow in the repository today. Validate locally before merging.

## Docker and Compose

`docker-compose.yml` runs API + Admin UI with Development environment and a shared `./data` volume.

Production-oriented deployments should:

- Set `Persistence` to MongoDB and/or Redis (see [Persistence guide](persistence-guide.md))
- Mount secrets via environment variables, not committed JSON
- Place API and Admin UI behind TLS termination
- Restrict network access to the API

Dockerfiles:

| File | Image |
| --- | --- |
| `ClientManager.Api/Dockerfile` | `clientmanager/api:local` |
| `ClientManager.AdminUI/Dockerfile` | `clientmanager/adminui:local` |

## Logging

Both hosts use **NLog** (`nlog.config`). API request tracking flows through `RequestTrackingMiddleware`; domain errors through `ErrorHandlingMiddleware` as `problem+json`.

Every error body includes `traceId` — match it to:

- NLog request logs
- OpenTelemetry spans (`storage.access.check`, `storage.resource.acquire`, …)
- Prometheus counters tagged by denial reason

Optional Elasticsearch sink: `Logging:Elasticsearch:Uri` in API configuration.

## Multi-instance notes

| Concern | Guidance |
| --- | --- |
| Shared rate-limit state | `RateLimiting` and `Allocations` roles should use Redis or another shared backend |
| Configuration | MongoDB recommended; JsonFile on NFS is possible but not ideal |
| Cache | Each API instance has its own in-memory `StorageReadCache`; TTL-bound staleness is possible |
| Usage buffers | Each instance buffers usage in memory; snapshots merge in the `Statistics` role |

## Troubleshooting

### Empty dashboard / no clients

- Persistence directory is empty or wrong working directory.
- Fix: seed via script or `Seed` config; confirm `./data` or configured `DataDirectory`.

### `503 Service Unavailable`

- Storage backend unreachable (Redis down, Mongo connection failure, file permission error).
- Check API startup logs for persistence binding lines.

### Client gets `429` but limit looks fine in the UI

- Check **global** rate limits on the service (aggregate exhaustion).
- Check client `exemptFromGlobalLimits` / `contributesToGlobalLimits` flags.
- Remember: `GET /access/check` **consumes** quota — do not use it as a monitoring poll.

### `401` vs `403`

- `401` — no `services[serviceId]` entry (not configured).
- `403` — client disabled, service disabled, or `isAllowed: false`.

### Slots not released after crash

- `AllocationCleanupService` reclaims expired slots (~30 s interval) but does **not** emit `Released` usage events.
- Integrators should release in `finally`; rely on TTL only as a safety net.

### Slow access checks (>250 ms logged)

- Often storage latency or cache cold start.
- Review persistence provider choice and hot-path metrics at `/prometheus/otel`.

### Admin UI cannot connect

- API not running, or `ApiBaseUrl` mismatch.
- Docker: Admin UI must use `http://api:5062`, not `localhost`.

## License

The project is licensed under **GNU GPL v3** (`LICENSE`). Relevant if you distribute modified versions or link proprietary services — not legal advice; read the license file.

## Related reading

- [Configuration reference](configuration-reference.md)
- [Persistence guide](persistence-guide.md)
- [Getting started](getting-started.md)
- [Usage and observability](core/usage-and-observability.md)
