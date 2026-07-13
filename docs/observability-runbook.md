# Observability runbook

Checked-in stack under [`observability/`](../observability/) and [`compose/observability.yml`](observability.yml).

## What to run when

| Goal | Command |
| --- | --- |
| API only (host) | `dotnet run --project ClientManager.Api` |
| API + Admin UI | `docker compose up` (default includes) |
| Metrics dashboard only | Include `compose/observability.yml`, then `docker compose up` |
| Multipod + dashboard | Include `multipod.yml` + `observability.yml`, then `docker compose up` |
| + traces locally | `docker compose --profile traces up` |
| + synthetic load | Include `traffic-gen.yml`, then `docker compose --profile load up` |
| Convenience wrapper | `python _scripts/launch_observability_ui.py up` |
| + traces via wrapper | `python _scripts/launch_observability_ui.py up --traces` |
| **Production** | Import dashboard JSON + give platform team scrape URL (below) |

## Endpoints (local compose)

| Service | URL |
| --- | --- |
| Grafana | http://localhost:3000/d/clientmanager-observability |
| Prometheus | http://localhost:9090 |
| Tempo (`--profile traces`) | http://localhost:3200 (also embedded in dashboard **Traces** row) |
| OTLP (`--profile traces`) | `http://localhost:4317` → set `Observability__OtlpEndpoint` on API |

## Dashboard zones

- **Global — all replicas:** sums every scraped pod; does not change when you pick a pod.
- **Storage zone**: per-role subsections with ops/s and latency graphs; **Storage pod** = *All* or one pod.
- **Traces**: trace table at the bottom — click a Trace ID for the waterfall. Requires `--profile traces`.
- **Pod — $pod:** entire lower section filters `instance="$pod"`. Toggle the pod dropdown to compare replicas.

Regenerate dashboard after metric changes:

```powershell
python _scripts/build_observability_dashboard.py
```

## Multipod demo (skewed load)

```powershell
# docker-compose.yml includes: multipod.yml, observability.yml, traffic-gen.yml
$env:TRAFFIC_WEIGHTS = "api-1:80,api-2:15,api-3:5"
docker compose --profile load up
```

Switch `$pod` in Grafana — per-pod zone changes; global zone stays aggregated.

## Production (org Grafana + Prometheus)

1. Platform team scrapes `https://<route>/prometheus/otel` (suggest 15–60s interval).
2. Import [`observability/grafana/dashboards/clientmanager.json`](../observability/grafana/dashboards/clientmanager.json) into org Grafana.
3. Map the `datasource` variable to your org Prometheus UID on import.
4. Share [`metrics-catalog.md`](metrics-catalog.md) for cardinality and scrape checklist.

You do **not** run this compose stack in prod unless you want a local mirror.

### Scrape checklist for platform team

| Setup | OK? |
| --- | --- |
| Per-pod / pod-discovery scrape | Yes — full dashboard |
| Single sticky URL to one pod | Partial — ratios/latency OK; global volumes ~1/X |
| Round-robin single URL | **No** — invalid counter rates |

## Alerting (local stack)

Rules live in [`observability/prometheus/alerts.yml`](../observability/prometheus/alerts.yml). Org teams can reuse the same PromQL in their Alertmanager.
