# Pod discovery

Prometheus must scrape **one target per ClientManager API replica**. The dashboard assumes all targets share `job="clientmanager-api"` and differ by `instance` (host:port).

Common scrape settings:

| Setting | Value |
| --- | --- |
| `metrics_path` | `/prometheus/otel` |
| `job_name` | `clientmanager-api` (recommended — dashboard filters on this) |
| Port | `5062` unless your deployment maps another port |

## Scrape fidelity

| Pattern | OK? | Notes |
| --- | --- | --- |
| Pod discovery / one target per replica | Yes | Full dashboard |
| Static list of replica URLs | Yes | Fine for small, stable fleets |
| Single sticky URL to one pod | Partial | SLO ratios work; global volume panels under-count |
| Round-robin load balancer on one URL | **No** | Invalid counter `rate()` |

---

## Kubernetes

Expose port **5062** on a `Service` (or scrape pod IP directly). Restrict scrapes to the cluster network.

### PodMonitor (Prometheus Operator)

```yaml
apiVersion: monitoring.coreos.com/v1
kind: PodMonitor
metadata:
  name: clientmanager-api
  namespace: clientmanager
  labels:
    release: prometheus    # match your Prometheus Operator selector
spec:
  selector:
    matchLabels:
      app: clientmanager-api
  podMetricsEndpoints:
    - port: http-metrics
      path: /prometheus/otel
      interval: 30s
```

Pod template should expose a named port:

```yaml
ports:
  - name: http-metrics
    containerPort: 5062
```

### ServiceMonitor (scrape via Service endpoints)

Use when you prefer endpoint discovery over per-pod IP:

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: clientmanager-api
  namespace: clientmanager
  labels:
    release: prometheus
spec:
  selector:
    matchLabels:
      app: clientmanager-api
  endpoints:
    - port: http-metrics
      path: /prometheus/otel
      interval: 30s
```

Service definition:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: clientmanager-api
  labels:
    app: clientmanager-api
spec:
  selector:
    app: clientmanager-api
  ports:
    - name: http-metrics
      port: 5062
      targetPort: 5062
```

Verify in Prometheus: **one target per running pod**, labels include `job`, `instance`, `pod` (if kube-prometheus adds it).

---

## OpenShift

OpenShift ships the Prometheus Operator (User Workload Monitoring or platform monitoring). Use `ServiceMonitor` in the **monitored namespace** with a label selector your `Prometheus` CR matches.

### ServiceMonitor (User Workload Monitoring example)

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: clientmanager-api
  namespace: clientmanager
  labels:
    app: clientmanager-api
spec:
  selector:
    matchLabels:
      app: clientmanager-api
  endpoints:
    - port: http-metrics
      path: /prometheus/otel
      interval: 30s
      scheme: http
      tlsConfig:
        insecureSkipVerify: true   # only if scraping plain HTTP inside the mesh
```

Enable monitoring for the namespace per your cluster policy (`openshift.io/cluster-monitoring` label or UWM `enableUserWorkload` configuration).

### Route-based scrape (discouraged)

If you must scrape through an OpenShift `Route`, use **one Route per replica** or prefer `PodMonitor`/`ServiceMonitor`. A single Route with round-robin breaks counter rates.

### NetworkPolicy

Allow ingress from the prometheus namespace to port **5062** on ClientManager pods.

---

## Docker Compose

The checked-in local stack uses **static targets** on the Docker network. From `observability/prometheus/prometheus.yml`:

```yaml
scrape_configs:
  - job_name: clientmanager-api
    metrics_path: /prometheus/otel
    static_configs:
      - targets:
          - api-1:5062
          - api-2:5062
          - api-3:5062
```

| Layout | Targets |
| --- | --- |
| Single-pod (`compose/default.yml`) | `api:5062` |
| Multipod (`compose/multipod.yml`) | `api-1:5062`, `api-2:5062`, `api-3:5062` |
| API on host, observability in Docker | `host.docker.internal:5062` |

When API and Prometheus run in **different** compose projects, use reachable hostnames or attach both to a shared external network — Docker DNS names are not cross-project by default.

---

## Bare metal / VMs

### Static config

```yaml
scrape_configs:
  - job_name: clientmanager-api
    metrics_path: /prometheus/otel
    static_configs:
      - targets:
          - 10.0.1.11:5062
          - 10.0.1.12:5062
          - 10.0.1.13:5062
        labels:
          env: production
```

### file_sd (dynamic fleet)

`prometheus/targets/clientmanager.json`:

```json
[
  {
    "targets": ["10.0.1.11:5062", "10.0.1.12:5062"],
    "labels": { "job": "clientmanager-api", "env": "production" }
  }
]
```

`prometheus.yml`:

```yaml
scrape_configs:
  - job_name: clientmanager-api
    metrics_path: /prometheus/otel
    file_sd_configs:
      - files:
          - /etc/prometheus/targets/clientmanager.json
        refresh_interval: 30s
```

Regenerate the JSON from your CMDB or orchestration tool when nodes join or leave.

---

## Relabeling tips

Keep `job="clientmanager-api"` on the final series. If your platform assigns a different job name, either:

- Relabel to `clientmanager-api`:

```yaml
relabel_configs:
  - source_labels: [__address__]
    target_label: instance
  - target_label: job
    replacement: clientmanager-api
```

- Or regenerate the dashboard with a different `JOB` constant in `_scripts/build_observability_dashboard.py` (last resort).

## Verify discovery

```promql
up{job="clientmanager-api"}
```

Expect **1** per replica. Then:

```promql
count by (instance) (clientmanager_http_requests_total)
```

Each `instance` should report independently.

## Related

- [Local development](local.md) — compose static targets in practice
- [On-prem observability stack](on-prem-stack.md) — deploy Prometheus yourself
- [Existing Grafana & Prometheus](existing-monitoring.md) — import dashboard into org Grafana
- [Metrics catalog](../metrics-catalog.md)
