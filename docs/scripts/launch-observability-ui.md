# launch_observability_ui.py

Starts the checked-in Prometheus + Grafana stack from `compose/observability.yml`.

Full walkthrough: [Local observability](../observability/local.md) (Path 1).

## Usage

```powershell
python _scripts/launch_observability_ui.py up
python _scripts/launch_observability_ui.py up --traces
python _scripts/launch_observability_ui.py down
```

| URL | Default |
| --- | --- |
| Grafana | http://localhost:3000/d/clientmanager-observability |
| Prometheus | http://localhost:9090 |
| Tempo (`--traces`) | http://localhost:3200 |

See `python _scripts/launch_observability_ui.py --help` for port and browser flags.

## Related

- [Observability guides](../observability/index.md)
- [Metrics catalog](../metrics-catalog.md)
