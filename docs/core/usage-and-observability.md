# Usage and observability

ClientManager records what happens on the hot path — grants, denials, acquisitions, releases — and turns that stream into dashboard charts, monitor views, and exportable metrics. This page explains the pipeline from in-memory events to the statistics API your operators (and the Admin UI) consume.

## What gets recorded

Runtime services call `IUsageRecorder` at decision points:

| Event | When | Target type |
| --- | --- | --- |
| `Granted` | Access check succeeds | `Service` |
| `Denied` | Access check fails (after identity/config resolved) | `Service` |
| `Acquired` | Resource slot successfully acquired | `ResourcePool` |
| `Released` | Explicit release via API | `ResourcePool` |

TTL-based cleanup of expired allocations **does not** produce a `Released` event. Operational counts for releases only reflect explicit API calls.

Each event is keyed by `(clientId, targetType, targetId, eventType)` and buffered in memory.

## From buffer to snapshots

Usage recording is deliberately decoupled from persistence so access checks stay fast:

```mermaid
flowchart LR
    hot[AccessControlService<br/>ResourceAllocationService] --> rec[UsageRecorder]
    rec --> buf[UsageBuffer<br/>in-memory per pod]
    buf --> persist[UsagePersistenceService<br/>background worker]
    persist --> counters[(Atomic usage counters<br/>Statistics role)]
    counters --> snap[(UsageSnapshot documents)]
    snap --> pre[StatisticsPrecomputeService]
    pre --> meta[(Precomputed overview + gauges)]
    snap --> ts[StatisticsTimeseriesService]
    meta --> api[Statistics API + Admin UI]
    ts --> api
```

The fast flush loop writes granted/denied/released counts to **atomic TTL-backed counters** (safe across multiple API pods). The slow rollup loop folds counters into snapshot documents and prunes expired buckets on every instance.

Snapshots store time-bucketed counts at multiple **granularities**:

| Granularity | Typical use |
| --- | --- |
| `Second` | Highest resolution; shortest retention |
| `FiveMinute` | Operational dashboards |
| `Hour` | Medium-term capacity planning |
| `Day` | Long-term trend analysis |

The system maintains separate snapshot series per granularity so the Admin UI can zoom from seconds to days without re-aggregating raw events on every query.

## Statistics services

Two services sit above the snapshot store:

### `StatisticsPrecomputeService` (write path)

Runs during `UsagePersistenceService` flush and rollup cycles. Maintains:

- `overview-summary` — RPM and pool acquisition gauges for `GET /statistics/overview`
- `latest-usage-gauges` — per service×client granted/denied for Prometheus/Grafana export

### `StatisticsTimeseriesService` (read path)

Serves `POST /statistics/timeseries/search` with a single rollup-tier read per request (no granularity fallback loop). Picks the coarsest stored tier that still satisfies the requested range and `bucketCount`, merges buckets server-side, and overlays live counters for the recent tail.

### `StatisticsService` (public API facade)

What controllers expose under `/api/v1/statistics/*`:

- `GET /overview` — counts, active allocations, RPM, pool acquisition (from precomputed summary + catalog counts)
- `POST /timeseries/search` — chart-ready bucketed series (`searchCategory`: `ServiceRequests`, `ResourcePoolAllocations`, `ResourcePoolRequests`)

The Admin UI dashboard, monitor, and allocations pages call these endpoints through `StatisticsApiService` (≤4 requests per dashboard poll).

## Read-only vs mutating queries

| Endpoint style | Increments counters? | Records usage? |
| --- | --- | --- |
| `GET /access/check` | Yes | Yes (`Granted` / `Denied`) |
| `GET /access/{clientId}` | No | No |
| `GET /resources/acquire` | Yes (pool limits) | Yes (`Acquired`) |
| `GET /resources/release` | No (decrements slot counters) | Yes (`Released`) |
| `GET /statistics/*` | No | No |

When building custom monitoring, prefer statistics and accessibility endpoints over repeated access checks.

## Caching

`IStorageReadCache` / `StorageReadCache` provides read-through caching with separate TTLs for:

- **Catalog reads** — clients, services, pools, global limit rules
- **Statistics closed base** — snapshot aggregates keyed without wall-clock `toUtc`; invalidated on rollup only
- **Live overlay** — batched pending-counter read applied on every live (`toUtc ≈ now`) request

Catalog writes invalidate catalog cache only. Statistics closed cache is invalidated on the slow usage rollup loop; live tail freshness comes from a single `usage:` counter prefix overlay at read time (not per-flush cache busting).

Hot-path configuration reads benefit from caching; usage writes go to the in-memory buffer first, so recording does not block on snapshot persistence.

## Metrics and tracing

ClientManager emits OpenTelemetry runtime metrics, usage/capacity gauges, and OTLP traces. The statistics API and Admin UI charts are separate from external monitoring.

**For Prometheus, Grafana, Jaeger, scrape configs, metric names, and example alerts**, see the dedicated [Metrics integration guide](../metrics-integration-guide.md).

At a glance:

| Path | Purpose |
| --- | --- |
| `/prometheus/otel` | Runtime counters and histograms (access, rate limits, HTTP, storage latency) |
| `/api/v1/metrics/prometheus` | Usage and pool-capacity gauges from precomputed documents |
| `Observability:OtlpEndpoint` | OTLP trace export to Jaeger, Tempo, etc. |

Hot-path spans use operation names like `storage.access.check` and `storage.resource.acquire`, tagged with `client.id`, `service.id`, and `resource_pool.id`.

## Admin UI surfaces

The Blazor Admin UI visualizes the same data without direct database access:

| Page | Data source |
| --- | --- |
| **Dashboard** (`/`) | System overview, per-client usage charts, filterable time ranges |
| **Monitor** | Live accessibility report per client |
| **Active allocations** | Current pool slot holders |
| **Entity editors** | Catalog CRUD via respective API services |

Chart polling intervals and axis scale preferences are stored client-side in `UserPreferencesService`.

## Problem responses and incident correlation

Every HTTP error from the API includes a `traceId` in the problem body. Match this value to:

- API request logs (NLog)
- OpenTelemetry trace IDs on hot-path spans
- Prometheus counters tagged by denial reason

When a tenant reports unexpected `429` responses, check both per-client limits and global limits for the target service — aggregate exhaustion can deny clients who are individually under quota.

## Helper scripts

See **[Scripts](../scripts/index.md)** for full documentation. Quick start:

```powershell
# Seed catalog configuration
python _scripts/seed_data.py --base-url http://localhost:5062

# Generate live traffic for dashboard testing
python _scripts/traffic_generator.py --base-url http://localhost:5062 --interval 2.0
```

Stop the traffic generator before shutting down the API so buffered usage events can flush cleanly.

## Related reading

- [Metrics integration guide](../metrics-integration-guide.md) — Prometheus, Grafana, Jaeger, OTLP
- [Request flow](request-flow.md) — when each event type is emitted
- [Domain model](domain-model.md) — targets and limits that shape usage patterns
- [Architecture overview](architecture.md) — background workers and observability endpoints
- [Persistence overview](../persistence/index.md) — `Statistics` storage role and snapshot layout
