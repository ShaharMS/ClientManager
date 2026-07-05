# Scripts overview

Python helpers in [`_scripts/`](../../_scripts/) support local development, demos, UI stress testing, observability, and performance baselines. They share catalogs, URLs, and paths through [`configuration.py`](../../_scripts/configuration.py).

## Prerequisites

- Python 3.10+ (stdlib only for most scripts)
- [`ClientManager.Api`](../../ClientManager.Api/) running when a script calls the public API (default `http://localhost:5062`)
- Run scripts from the repository root or `_scripts/` directory

## Shared configuration

[`configuration.py`](../../_scripts/configuration.py) defines:

| Area | Purpose |
| --- | --- |
| `global.api` | Base URL and API prefix |
| `global.data` | Default data directories and collection file names |
| `global.catalogs` | Demo clients, services, pools used by seed and traffic scripts |
| `scripts.*` | Per-script defaults (intervals, presets, probabilities) |

Override the API URL with `--base-url` on scripts that support it.

Set `CLIENTMANAGER_STORAGE_DATA_DIR` to point the API at a custom persistence directory.

## Script index

| Script | API required? | Purpose |
| --- | --- | --- |
| [seed_data.py](seed-data.md) | Yes | Seed catalog via API + optional historical usage files |
| [traffic_generator.py](traffic-generator.md) | Yes | Continuous live traffic for demos |
| [performance_baseline.py](performance-baseline.md) | Yes | Deterministic load profile + latency report |
| [launch_observability_ui.py](launch-observability-ui.md) | No | Local Grafana, Prometheus, Jaeger stack |
| [download_images.py](download-images.md) | No | Pull/build Docker images for deployment |

## Typical workflows

### Demo dashboard with realistic traffic

1. Start `ClientManager.Api`
2. `python _scripts/seed_data.py`
3. `python _scripts/traffic_generator.py`
4. Start `ClientManager.AdminUI`

## Related reading

- [Development and operations](../development-and-operations.md)
- [Getting started](../getting-started.md)
- [Usage and observability](../core/usage-and-observability.md)
