# launch_observability_ui.py

Starts the **checked-in** Prometheus + Grafana stack from [`compose/observability.yml`](../compose/observability.yml).

## Default endpoints

| Service | URL |
| --- | --- |
| Grafana | http://localhost:3000/d/clientmanager-observability |
| Prometheus | http://localhost:9090 |
| Tempo (`--traces`) | http://localhost:3200 |

## Usage

```powershell
python _scripts/launch_observability_ui.py up
python _scripts/launch_observability_ui.py up --traces
python _scripts/launch_observability_ui.py down
```

Running without a subcommand starts the metrics stack. See `python _scripts/launch_observability_ui.py --help`.

Provisioning and dashboards live under [`observability/`](../observability/) (version controlled).

## Related

- [Observability runbook](../observability-runbook.md)
- [Metrics catalog](../metrics-catalog.md)
- [Metrics integration guide](../metrics-integration-guide.md)
