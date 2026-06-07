# Getting started

This page is for someone opening the repository for the first time. It covers what the solution contains, how to run it locally, and where to read next.

## What ClientManager is

ClientManager is a .NET service that answers operational questions at request time:

- May this **client** call this **service**?
- Is the client under its **rate limit**?
- Can the client **hold a slot** in a **resource pool**?

Your applications (or a reverse proxy in front of them) call the HTTP API on every inbound request. Operators configure clients, services, pools, and limits through the **Admin UI** or the **catalog API**.

ClientManager is **not** a user directory. It does not authenticate end users or issue tokens. You supply a stable `clientId` on each request; see the [Integration guide](integration-guide.md).

## Solution layout

The active solution (`ClientManager.slnx`) contains:

| Project | Type | Purpose |
| --- | --- | --- |
| `ClientManager.Api` | Executable | Public HTTP API, all business logic, in-process persistence, background workers |
| `ClientManager.AdminUI` | Executable | Blazor Server admin dashboard; talks to the API over HTTP only |
| `ClientManager.DataAccess` | Library | Document stores and repositories (referenced **only** by the API) |
| `ClientManager.Shared` | Library | Entities, DTOs, enums, configuration models, logging |
| `ClientManager.DataAccess.Tests` | Test harness | Console verification for JsonFile storage (not xUnit) |
| `ClientManager.DependencyInventory` | Tooling | Dependency audit helper under `_Solution Items_/Bookkeeping` |

### Folders outside the solution

| Path | Notes |
| --- | --- |
| `docs/` | MkDocs documentation (this site) |
| `_scripts/` | Python helpers for seeding, traffic, observability, performance baselines |
| `data/` | Default JsonFile persistence directory when running locally |
| `docker-compose.yml` | API + Admin UI containers with `./data` mounted |
| `site/` | Built MkDocs output (`mkdocs build`); safe to regenerate |

## Requirements

- **.NET SDK 10.0** or later
- **Python 3** for `_scripts/` helpers
- **pip** if you want to build or preview the doc site

Optional for full local observability:

- **Docker** for `docker-compose.yml`, `download_images.py`, and `launch_observability_ui.py`

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

Out of the box the API uses **JsonFile** persistence with data in `./data` (relative to the API working directory). Catalogs may be empty on first run.

Populate realistic demo configuration:

```powershell
python _scripts/seed_data.py --base-url http://localhost:5062
```

The seed script mirrors the catalogs defined in `_scripts/configuration.py` (20 services, 10 resource pools, global limits, and several client profiles).

Generate live traffic for dashboard testing:

```powershell
python _scripts/traffic_generator.py --base-url http://localhost:5062 --interval 2.0
```

Stop the traffic generator before stopping the API so buffered usage events can flush.

## Docker

```powershell
docker compose up --build
```

- API: `http://localhost:5062`
- Admin UI: `http://localhost:5100`
- Persistence: `./data` mounted into the API container

Build project images manually:

```powershell
python _scripts/download_images.py --build-projects --build-version 1.0.1-alpha
```

Use `--list` to preview without running Docker.

## Verify persistence layer

`ClientManager.DataAccess.Tests` is a **console program**, not a standard test project:

```powershell
dotnet run --project ClientManager.DataAccess.Tests
```

It verifies JsonFile counter and document round-trips. Expect `JsonFile storage verification passed.` on success.

## Documentation map

Read in this order if you are new:

| Order | Guide | Why |
| --- | --- | --- |
| 1 | [Architecture overview](core/architecture.md) | Hosts, layering, background workers |
| 2 | [Domain model](core/domain-model.md) | Clients, services, pools, limits |
| 3 | [Request flow](core/request-flow.md) | Hot-path behavior and HTTP statuses |
| 4 | [Integration guide](integration-guide.md) | Wire ClientManager in front of your services |
| 5 | [Configuration reference](configuration-reference.md) | Every `appsettings` section and default |
| 6 | [Admin UI guide](admin-ui-guide.md) | Operator screens and typical workflows |
| 7 | [API overview](api-overview.md) | Endpoint groups beyond the four gatekeeping calls |
| 8 | [Development and operations](development-and-operations.md) | Scripts, observability, security, troubleshooting |
| 9 | [Persistence guide](persistence-guide.md) | Storage roles and provider topologies |

The root [README.md](../README.md) duplicates quick-start commands and links back here.

## Build the doc site

```powershell
pip install -r docs/requirements.txt
mkdocs serve    # http://127.0.0.1:8000
mkdocs build    # output in site/
```

To add a page: create `docs/your-page.md`, add it to `nav` in `mkdocs.yml`, and link it from [index.md](index.md) if it should appear on the home page.
