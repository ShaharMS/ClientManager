# Observability

ClientManager exports OpenTelemetry metrics at `GET /prometheus/otel` and optional OTLP traces via `Observability:OtlpEndpoint`. The checked-in stack under `observability/` provides Prometheus, Grafana, and (optionally) Tempo for local and on-prem use.

## Guides

| Guide | When to use it |
| --- | --- |
| [Local development](local.md) | Run ClientManager on your machine — host, single-pod compose, or multipod compose with optional load and traces |
| [On-prem observability stack](on-prem-stack.md) | Deploy Prometheus + Grafana (+ Tempo) yourself and point them at a deployed ClientManager |
| [Existing Grafana & Prometheus](existing-monitoring.md) | Your platform team already runs monitoring — add scrape targets and import the dashboard |
| [Pod discovery](pod-discovery.md) | Per-platform scrape config (Kubernetes, OpenShift, Docker Compose, VMs) |

## Reference

| Doc | Purpose |
| --- | --- |
| [Metrics catalog](../metrics-catalog.md) | Metric names, labels, cardinality, scrape checklist |
| [Usage and observability](../core/usage-and-observability.md) | RPM pipeline, statistics API, caching |
| [launch_observability_ui.py](../scripts/launch-observability-ui.md) | Convenience wrapper for the local metrics stack |

## Repository assets

| Asset | Path (repo root) |
| --- | --- |
| Dashboard JSON | `observability/grafana/dashboards/clientmanager.json` |
| Dashboard generator | `_scripts/build_observability_dashboard.py` |
| Prometheus config | `observability/prometheus/prometheus.yml` |
| Alert rules | `observability/prometheus/alerts.yml` |
| Compose stack | `compose/observability.yml` |
