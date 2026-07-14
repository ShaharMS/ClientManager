# Getting started

This page is for someone opening the repository for the first time. It covers what the solution contains, how to run it locally, and where to read next.

## What ClientManager is

ClientManager is a .NET service that answers operational questions at request time:

- May this **client** call this **service**?
- Is the client under its **rate limit**?

Your applications (or a reverse proxy in front of them) call the HTTP API on every inbound request. Operators configure clients, services, and limits through the **Admin UI** or the **catalog API**.

ClientManager is **not** a user directory. It does not authenticate end users or issue tokens. You supply a stable `clientId` on each request; see the [Integration guide](integration-guide.md).

## Solution layout

The active solution (`ClientManager.slnx`) contains:

| Project | Type | Purpose |
| --- | --- | --- |
| `ClientManager.Api` | Executable | Public HTTP API, business logic, in-process storage (`Api/Storage`), background workers |
| `ClientManager.AdminUI` | Executable | Blazor Server admin dashboard; talks to the API over HTTP only |
| `ClientManager.Shared` | Library | Entities, DTOs, enums, configuration models, logging |
| `ClientManager.Tests` | Test | Unit and integration regression suite |
| `ClientManager.DependencyInventory` | Tooling | Dependency audit helper under `_Solution Items_/Bookkeeping` |

### Folders outside the solution

| Path | Notes |
| --- | --- |
| `docs/` | MkDocs documentation (this site) |
| `_scripts/` | Python helpers for seeding, traffic, observability, performance baselines |
| `data/` | Legacy local dev artifacts; not used when Redis/Mongo is configured |
| `docker-compose.yml` | Entry point for `docker compose up` — edit `include` to switch stacks |
| `compose/default.yml` | API + Admin UI containers with `./data` mounted |
| `compose/dev.redis.yml` | Redis overlay (combine with `default.yml`) |
| `compose/redis.yml` | Standalone Redis for local dev and integration tests |
| `compose/multipod.yml` | Three API replicas + MongoDB + Redis for multi-pod testing |
| `site/` | Built MkDocs output (`mkdocs build`); safe to regenerate |

## Requirements

- **.NET SDK 10.0** or later
- **Python 3** for `_scripts/` helpers
- **pip** if you want to build or preview the doc site

Optional for full local observability:

- **Docker** for `compose/` stacks, `download_images.py`, and `launch_observability_ui.py`

## Build

```powershell
dotnet restore ClientManager.slnx
dotnet build ClientManager.slnx
```

## Run locally

Start hosts **bottom-up**:

1. **API** — `http://localhost:5062`
2. **Admin UI** — `http://localhost:5100`

```powershell
# Terminal 1
dotnet run --project ClientManager.Api

# Terminal 2
dotnet run --project ClientManager.AdminUI
```

The Admin UI reads `ApiBaseUrl` from `ClientManager.AdminUI/appsettings.json` (default `http://localhost:5062`).

### Interactive API docs

With the API running, open Swagger UI at [http://localhost:5062/docs](http://localhost:5062/docs). This is the most complete reference for catalog CRUD and statistics endpoints.

### Seed demo data

Out of the box the API uses **Redis** persistence on `localhost:6379` (see `ClientManager.Api/appsettings.Development.json`). Start Redis with `docker compose -f compose/redis.yml up -d` if needed. Catalogs may be empty on first run.

Populate realistic demo configuration:

```powershell
python _scripts/seed_data.py --base-url http://localhost:5062
```

The seed script mirrors the catalogs defined in `_scripts/configuration.py` (services, global limits, and several client profiles).

For **catalog-only** seeding without the script, use the seed API (`GET` / `POST` / `PUT` `/api/v2/seed`) or the appsettings `Seed` section — see [Seed system](core/seed-system.md).

Generate live traffic for dashboard testing:

```powershell
python _scripts/traffic_generator.py --base-url http://localhost:5062 --interval 2.0
```

Stop the traffic generator before stopping the API so buffered usage events can flush.

## Docker

```powershell
docker compose up --build
```

Switch stacks by editing `include` in repo-root `docker-compose.yml` (see `compose/README.md` in the repository).

- API: `http://localhost:5062`
- Admin UI: `http://localhost:5100`
- Persistence: `./data` mounted into the API container

Build project images manually:

```powershell
python _scripts/download_images.py --build-projects --build-version 2.0.0
```

Use `--list` to preview without running Docker.

## Documentation map

Read in this order if you are new:

| Order | Guide | Why |
| --- | --- | --- |
| 1 | [Architecture overview](core/architecture.md) | Hosts, layering, background workers |
| 2 | [Domain model](core/domain-model.md) | Clients, services, and rate limits |
| 3 | [Request flow](core/request-flow.md) | Hot-path behavior and HTTP statuses |
| 4 | [Integration guide](integration-guide.md) | Wire ClientManager in front of your services |
| 5 | [Configuration reference](configuration-reference.md) | Every `appsettings` section and default |
| 6 | [Admin UI guide](admin-ui-guide.md) | Operator screens and typical workflows |
| 7 | [API overview](api-overview.md) | Catalog, statistics, seed, and metrics endpoints |
| 8 | [Observability guides](observability/index.md) | Local stack, on-prem deploy, org Grafana/Prometheus |
| 9 | [Development and operations](development-and-operations.md) | Scripts, security, troubleshooting |
| 10 | [Persistence overview](persistence/index.md) | Storage roles, provider comparison, and topologies |

The repository root `README.md` duplicates quick-start commands and links back here.

## Build the doc site

```powershell
pip install -r docs/requirements.txt
mkdocs serve    # http://127.0.0.1:8000
mkdocs build    # output in site/
```

Mermaid is vendored under `docs/javascripts/` for offline use. Run `mkdocs build` before serving the static `site/` folder in airgapped environments.

To add a page: create `docs/your-page.md`, add it to `nav` in `mkdocs.yml`, and link it from [index.md](index.md) if it should appear on the home page.
