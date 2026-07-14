# Existing Grafana & Prometheus

Your platform team already runs Grafana and Prometheus (on-prem or hosted). This guide adds ClientManager scrape targets, imports the dashboard, and validates per-pod discovery.

You do **not** deploy `compose/observability.yml` unless you want a local mirror.

## What you need from ClientManager

| Item | Value |
| --- | --- |
| Scrape path | `/prometheus/otel` |
| Recommended job name | `clientmanager-api` |
| Default port | `5062` |
| Auth | None — restrict by network policy / internal ingress only |

Each API replica must be scraped **individually**. See [Pod discovery](pod-discovery.md) for your platform.

## Step 1 — Verify metrics from one replica

```bash
curl -sS https://<clientmanager-host>/prometheus/otel | head
```

Expect Prometheus text exposition (`# HELP`, `# TYPE`, `clientmanager_*` series).

## Step 2 — Add Prometheus scrape config

Add a job to your org Prometheus config (or `PodMonitor` / `ServiceMonitor` — see [Pod discovery](pod-discovery.md)).

### Static targets (VMs or known hosts)

```yaml
scrape_configs:
  - job_name: clientmanager-api
    scrape_interval: 30s
    metrics_path: /prometheus/otel
    scheme: https
    static_configs:
      - targets:
          - cm-api-01.internal:5062
          - cm-api-02.internal:5062
```

### TLS / mTLS

If Prometheus scrapes through an internal ingress:

```yaml
  - job_name: clientmanager-api
    metrics_path: /prometheus/otel
    scheme: https
    tls_config:
      insecure_skip_verify: false
      ca_file: /etc/prometheus/tls/ca.pem
    static_configs:
      - targets: [clientmanager.internal:443]
```

Prefer **per-pod targets** behind the ingress (separate URLs or discovery) over a single round-robin VIP.

### On-prem Prometheus specifics

| Concern | Guidance |
| --- | --- |
| **Network** | Scraper must reach every replica; metrics endpoints are not authenticated |
| **Interval** | 15–60s is typical; local compose uses 5s for demos |
| **Retention** | ClientManager histograms are cumulative; keep enough retention for your SLO windows |
| **Cardinality** | Review [Metrics catalog](../metrics-catalog.md) before enabling high-cardinality labels in prod |
| **Federation** | If using federation, federate `clientmanager_*` metrics from a dedicated scrape job — do not aggregate at scrape time via a load balancer |

Reload Prometheus and confirm **Targets** show `clientmanager-api` **UP** for every replica.

## Step 3 — Import the dashboard {#step-3-import-the-dashboard}

### Step 3a — Regenerate (optional)

If you changed metrics locally or need a fresh export:

```powershell
python _scripts/build_observability_dashboard.py
```

Output: `observability/grafana/dashboards/clientmanager.json`

### Step 3b — Import into Grafana

1. **Dashboards → New → Import**
2. Upload `clientmanager.json` or paste its contents
3. When prompted for the **`datasource`** variable, map it to your org Prometheus datasource UID
4. Click **Import**

Dashboard UID: `clientmanager-observability`  
Direct URL pattern: `https://<grafana-host>/d/clientmanager-observability`

### Step 3c — Dashboard variables

| Variable | Source | Notes |
| --- | --- | --- |
| `datasource` | Prometheus | Must point at the datasource scraping ClientManager |
| `pod` | `label_values(..., instance)` | Per-replica filter for lower **Pod** section |
| `service` / `client` | Label values from `clientmanager_requests_total` | Multi-select filters |
| `storage_pod` | Replica instances | Storage subsection only |

Only the **Pod — $pod** zone reacts to the pod dropdown. **Global — all replicas** always aggregates every target in `job="clientmanager-api"`.

### Step 3d — Tempo (optional)

The dashboard includes a **Traces** row wired to datasource UID `clientmanager-tempo`. If your org does not use Tempo, that row stays empty — metrics panels work without it.

To enable traces: export OTLP from ClientManager (`Observability:OtlpEndpoint`) to your org collector/Tempo and add a Grafana Tempo datasource with UID `clientmanager-tempo`, or edit the dashboard to reference your Tempo UID.

## Step 4 — Validate panels

1. Generate traffic to ClientManager (access checks, not statistics polling)
2. Open the dashboard; set time range to **Last 15 minutes**
3. Confirm **HTTP req/s** and **Granted RPM** move
4. If multiple replicas: change **$pod** — lower section should shift; global section should not

### Scrape fidelity

| Setup | Result |
| --- | --- |
| Per-pod discovery | Full dashboard |
| Sticky single pod | Ratios OK; global counts under-report |
| Round-robin single URL | **Broken** `rate()` on counters — fix scrape layout |

## Step 5 — Alerts (optional)

Copy rules from `observability/prometheus/alerts.yml` into your Alertmanager/Prometheus rule set. All expressions filter `job="clientmanager-api"`.

**All targets down:**

```promql
count(up{job="clientmanager-api"} == 1) == 0
```

**High HTTP 5xx rate:**

```promql
sum(rate(clientmanager_http_requests_total{job="clientmanager-api",statusCode=~"5.."}[5m]))
  / sum(rate(clientmanager_http_requests_total{job="clientmanager-api"}[5m])) > 0.01
```

**Access denial rate:**

```promql
sum(rate(clientmanager_access_denied_total{job="clientmanager-api"}[5m]))
  / sum(rate(clientmanager_requests_total{job="clientmanager-api"}[5m])) > 0.25
```

## Step 6 — Hand off to operators

Share with the platform team:

1. Scrape URL pattern: `https://<route>/prometheus/otel`
2. [Pod discovery](pod-discovery.md) for their orchestrator
3. [Metrics catalog](../metrics-catalog.md) for cardinality review
4. Dashboard JSON path in this repo (or the imported Grafana URL)

## What not to scrape

| Endpoint | Why |
| --- | --- |
| `GET /api/v2/statistics/overview` | JSON operator stats, not Prometheus format |
| `GET /api/v2/access/check` | Consumes rate-limit quota |

For operator RPM without Prometheus, poll `/api/v2/statistics/overview` from the Admin UI — not from Prometheus.

## Related

- [Pod discovery](pod-discovery.md)
- [On-prem observability stack](on-prem-stack.md) — deploy our Prometheus/Grafana instead
- [Metrics catalog](../metrics-catalog.md)
