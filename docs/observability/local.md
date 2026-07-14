# Local observability

Step-by-step guide for developers running ClientManager on a workstation. Pick **one path** below; all three can share the same observability sidecar.

Checked-in assets: `compose/observability.yml`, `observability/`, `_scripts/launch_observability_ui.py`.

## Prerequisites

- Docker Desktop (or Docker Engine + Compose v2)
- .NET SDK (Path 1 only)
- Python 3 (seed/traffic scripts)

## Path 1 — API on the host + observability sidecar

Use this when you run `dotnet run` and want Prometheus/Grafana without rebuilding API images.

### Step 1 — Start the API

```powershell
dotnet run --project ClientManager.Api
```

Default base URL: `http://localhost:5062`.

### Step 2 — Start the observability stack

In a second terminal:

```powershell
python _scripts/launch_observability_ui.py up
```

Add `--traces` to also start Tempo and the OTel Collector:

```powershell
python _scripts/launch_observability_ui.py up --traces
```

| Flag | Effect |
| --- | --- |
| `--traces` | Starts `otel-collector` + `tempo` (`--profile traces`) |
| `--no-browser` | Skip opening Grafana |
| `--grafana-port` / `--prometheus-port` | Override defaults (3000 / 9090) |

Stop the stack:

```powershell
python _scripts/launch_observability_ui.py down
python _scripts/launch_observability_ui.py down --traces   # if traces were enabled
```

### Step 3 — Point Prometheus at the host API

The checked-in `observability/prometheus/prometheus.yml` lists Docker-network targets (`api-1`, `api-2`, …). For a host-run API, edit the file before starting the stack:

```yaml
scrape_configs:
  - job_name: clientmanager-api
    metrics_path: /prometheus/otel
    static_configs:
      - targets: [host.docker.internal:5062]
```

`compose/observability.yml` already maps `host.docker.internal` via `extra_hosts`.

Restart Prometheus after editing:

```powershell
docker compose -f compose/observability.yml restart prometheus
```

### Step 4 — (Optional) Enable traces

With `--traces` running, set OTLP on the API:

```powershell
$env:Observability__OtlpEndpoint = "http://localhost:4317"
dotnet run --project ClientManager.Api
```

Or use the script snippets:

```powershell
python _scripts/launch_observability_ui.py print-env
python _scripts/launch_observability_ui.py print-appsettings
```

### Step 5 — Seed data and generate traffic

```powershell
python _scripts/seed_data.py --base-url http://localhost:5062
python _scripts/traffic_generator.py --base-url http://localhost:5062 --interval 2.0
```

### Step 6 — Verify

| Check | Command / URL |
| --- | --- |
| Metrics endpoint | `curl -sS http://localhost:5062/prometheus/otel \| head` |
| Prometheus target | http://localhost:9090/targets — `clientmanager-api` should be **UP** |
| Grafana dashboard | http://localhost:3000/d/clientmanager-observability |
| Tempo (`--traces`) | http://localhost:3200/ready |

---

## Path 2 — Single-pod Docker Compose

One API replica + Admin UI. No MongoDB/Redis in this stack (in-memory/dev defaults).

### Step 1 — Select the compose include

Edit repo-root `docker-compose.yml`:

```yaml
include:
  - path: compose/default.yml
  - path: compose/observability.yml
```

### Step 2 — Start the stack

```powershell
docker compose up --build
```

Optional traces profile:

```powershell
docker compose --profile traces up --build
```

### Step 3 — Point Prometheus at the single API service

Edit `observability/prometheus/prometheus.yml`:

```yaml
static_configs:
  - targets: [api:5062]
```

Restart Prometheus:

```powershell
docker compose restart prometheus
```

### Step 4 — Open UIs

| Service | URL |
| --- | --- |
| API / Swagger | http://localhost:5062/docs |
| Admin UI | http://localhost:5100 |
| Grafana | http://localhost:3000/d/clientmanager-observability |
| Prometheus | http://localhost:9090 |

### Step 5 — Seed and verify

```powershell
python _scripts/seed_data.py --base-url http://localhost:5062
curl -sS http://localhost:5062/prometheus/otel | head
```

Confirm the `clientmanager-api` target is **UP** in Prometheus.

---

## Path 3 — Multipod Compose (+ optional load & traces)

Three API replicas sharing MongoDB + Redis. Matches production-like persistence and per-pod dashboard panels.

### Step 1 — Select the compose includes

Edit repo-root `docker-compose.yml`:

```yaml
include:
  - path: compose/multipod.yml
  - path: compose/observability.yml
  - path: compose/traffic-gen.yml   # optional — needed for --profile load
```

The repo default checked in today uses this full set.

### Step 2 — Start the stack

```powershell
docker compose up --build
```

Combine profiles as needed:

```powershell
docker compose --profile traces up --build
docker compose --profile load up --build
docker compose --profile traces --profile load up --build
```

| Profile | Services added | Purpose |
| --- | --- | --- |
| *(default)* | `api-1`, `api-2`, `api-3`, MongoDB, Redis, Admin UI, Prometheus, Grafana | Metrics + multipod |
| `traces` | `otel-collector`, `tempo` | Distributed tracing |
| `load` | `traffic-gen` | Synthetic access-check traffic |

Multipod compose already sets `Observability__OtlpEndpoint: http://otel-collector:4317` on each API pod (active when `traces` profile is up).

### Step 3 — Confirm Prometheus targets

`observability/prometheus/prometheus.yml` ships with:

```yaml
targets:
  - api-1:5062
  - api-2:5062
  - api-3:5062
```

No edit needed for the default multipod layout. Check http://localhost:9090/targets — three targets, same job `clientmanager-api`.

Host port mapping:

| Pod | Host port |
| --- | --- |
| `api-1` | 5062 |
| `api-2` | 5063 |
| `api-3` | 5064 |
| Admin UI | 5100 |

### Step 4 — Seed catalog data

```powershell
python _scripts/seed_data.py --base-url http://localhost:5062
```

### Step 5 — (Optional) Skewed load demo

```powershell
$env:TRAFFIC_WEIGHTS = "api-1:80,api-2:15,api-3:5"
docker compose --profile load up
```

Other `traffic-gen` environment variables (set before `up`):

| Variable | Default | Meaning |
| --- | --- | --- |
| `TRAFFIC_TARGETS` | `http://api-1:5062,…` | Comma-separated base URLs |
| `TRAFFIC_RPS` | `20` | Aggregate requests per second |
| `TRAFFIC_DURATION` | `0` | Seconds to run (`0` = until stopped) |

### Step 6 — Automated multipod verification

```powershell
python _scripts/run_multipod_docker.py
```

| Flag | Effect |
| --- | --- |
| `--keep-up` | Leave stack running after checks |
| `--skip-check` | Start only, no seed/check |
| `--no-build` | Skip image rebuild |

### Step 7 — Read the dashboard

Open http://localhost:3000/d/clientmanager-observability

| Zone | Behavior |
| --- | --- |
| **Global — all replicas** | Aggregates every scraped pod; unchanged by the `$pod` dropdown |
| **Storage** | Per-role ops/s and latency; **Storage pod** = *All* or one replica |
| **Pod — $pod** | Lower section filters `instance="$pod"` — toggle to compare replicas |
| **Traces** (`--profile traces`) | Click Duration / Start time / Name on a row; waterfall loads on the right |

Switch `$pod` while load is skewed — global panels stay aggregated; per-pod section shows replica imbalance.

---

## Regenerate the dashboard

After metric or panel changes in `_scripts/build_observability_dashboard.py`:

```powershell
python _scripts/build_observability_dashboard.py
docker compose restart grafana
```

See [Existing Grafana & Prometheus — import the dashboard](existing-monitoring.md#step-3-import-the-dashboard) for org Grafana.

## Tear down

```powershell
docker compose down
docker compose down -v    # also remove Mongo/Redis volumes (multipod)
```

## Related

- [On-prem observability stack](on-prem-stack.md) — production deployment of the same stack
- [Pod discovery](pod-discovery.md) — scrape config reference
- [Metrics catalog](../metrics-catalog.md) — metric names and cardinality
