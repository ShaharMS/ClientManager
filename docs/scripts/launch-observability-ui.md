# launch_observability_ui.py

Starts a local Grafana, Prometheus, and Jaeger stack for ClientManager metrics and traces.

## Default endpoints

| Service | URL |
| --- | --- |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Jaeger | http://localhost:16686 |

## Usage

```powershell
python _scripts/launch_observability_ui.py
```

Running without a subcommand starts the full stack. See `python _scripts/launch_observability_ui.py --help` for subcommands (configure hosts OTLP, stop stack, etc.).

Generated compose files live under `_scripts/.observability-stack/` (gitignored).

## Related

- [Metrics integration guide](../metrics-integration-guide.md)
- [Usage and observability](../core/usage-and-observability.md)
