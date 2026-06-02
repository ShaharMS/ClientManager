# Plan: Observability & Performance Sidebar Tabs

## Status: 🔲 Not started

## Overview

Add two new sidebar tabs to `ClientManager.AdminUI`:

1. **Observability** — browse, search, and filter distributed traces. Layout mirrors the existing list pages (Services, Clients, Resource Pools): a search box (search traces by trace ID) and an ID-to-trace data grid. Each row is expandable to reveal a custom span-waterfall graphic for that trace.
2. **Performance** — visualize the API's and Storage API's latency over time plus their per-operation metrics. Inspired by Active Allocations but with **two** main charts instead of one: a *hot-path* latency chart and a *rest-of-service* latency chart. Both are driven by two dropdowns — one selecting the measured service (`ClientManager.Api` or `ClientManager.StorageApi`) and one selecting the aggregation (median / average / p90 / p95 / p99). Below the main charts, data-driven small square cards show per-operation latency over time (Storage shows `get` / `set` / `get_many` / `set_many` / …; API shows its own operations).

The current system emits OpenTelemetry traces via OTLP to an external **Jaeger** instance (started by `_scripts/launch_observability_ui.py`, UI on `http://localhost:16686`). The app does **not** store or expose traces/latency series itself. Rather than add a Prometheus dependency or build an in-app trace store (both rejected as bloat by the requester), **both tabs read directly from the already-running Jaeger instance**. Crucially, every storage operation already emits a distinct span (`storage.document_store.{operation}`) and hot-path operations emit spans from `AccessControlService`, so Jaeger alone has enough data to power the per-operation Performance cards by computing duration percentiles from sampled span durations.

The desired end state: a shared Jaeger client layer in AdminUI plus two new pages, all gracefully showing a friendly "Jaeger is not configured / not reachable" notice when the instance is unavailable.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [observability-performance-tabs-1-jaeger-foundation.md](.github/plans/observability-performance-tabs-1-jaeger-foundation.md) | Shared Jaeger client foundation: config, HttpClient, `JaegerApiService`, response models, and the reusable "Jaeger unavailable" notice. |
| 2 | [observability-performance-tabs-2-observability-page.md](.github/plans/observability-performance-tabs-2-observability-page.md) | Observability page: nav entry, `/observability` route, ID-to-trace grid with search, expandable rows rendering a custom span waterfall. |
| 3 | [observability-performance-tabs-3-performance-page.md](.github/plans/observability-performance-tabs-3-performance-page.md) | Performance page: nav entry, `/performance` route, two main latency charts (hot-path vs rest), service + aggregation dropdowns, and data-driven per-operation cards. |

## Key Decisions

- **Single data source: the existing Jaeger instance** — both tabs read from Jaeger's HTTP query API (`/api/services`, `/api/operations`, `/api/traces`, `/api/traces/{id}`). No Prometheus, no in-app trace store. This honors the requester's "don't bloat with a bunch of observability deployments" constraint.
- **No reference pattern for trace querying** — there is no existing Jaeger/trace-consuming code in the repo. The closest analogous code is the existing `*ApiService` classes (HttpClient wrappers) and the list/chart pages; sub-plans reference those for shape and conventions. This absence is recorded here as required.
- **Direct AdminUI → Jaeger calls, not a ClientManager.Api proxy** — AdminUI is Blazor **Interactive Server**, so its services run server-side and can call Jaeger directly without CORS. Routing through `ClientManager.Api` would add proxy-only controllers/services for an external dev tool and pollute the domain API; the requester flagged bloat, so the call stays in AdminUI. Jaeger base URL comes from config key `JaegerBaseUrl` (default `http://localhost:16686`), mirroring the existing `ApiBaseUrl` convention.
- **Friendly unavailability notice is mandatory** — both tabs must detect an unconfigured/unreachable Jaeger and render a clear, friendly notice explaining that `_scripts/launch_observability_ui.py` must be running, instead of an error/exception. A single shared component is built in Step 1 and reused by both pages.
- **Performance percentiles are derived from sampled span durations** — Jaeger stores traces (spans with `duration` in microseconds), not pre-aggregated metric series. The Performance tab flattens spans for the selected service, buckets them over the selected time range, and computes median/avg/p90/p95/p99 per bucket. This is sample-based and approximate (affected by trace sampling); documented as an explicit accuracy caveat in the UI.
- **Hot-path vs rest classification is an explicit, configurable allow-list** — "hot path" = spans whose `operationName` matches known hot-path patterns (default: `storage.document_store.*` and the `AccessControl*` / resource-acquire/release / rate-limit span names). Everything else is "rest". The classifier lives in the Performance sub-plan as a single small helper with the patterns in one place.
- **Aggregation options** — the aggregation dropdown exposes: Median (p50), Average, p90, p95, p99.
- **Per-operation cards are data-driven** — the cards are generated from Jaeger's reported operations for the selected service (`/api/operations?service=`), so Storage naturally shows `get` / `set` / `get_many` / `set_many` (etc.) and API shows its own operations without hard-coding the API metric list.
- **Reuse existing chart/time controls** — both pages reuse `ChartSettingsDropdown` (time range, refresh rate, axis scale), `TimeRangePreset`, `PollingIntervalPreset`, `RadzenChart`, and the visibility/polling timer pattern from `ActiveAllocations.razor`.

## Iteration Bootstrap

- **Iteration slug**: `observability-performance-tabs`
- **Required evidence**: `dotnet build ClientManager.AdminUI` succeeds; AdminUI runs; with Jaeger up (`python _scripts/launch_observability_ui.py up`) and traffic flowing, both new pages render real traces/latency; with Jaeger down, both pages render the friendly notice (not an error). Preserve screenshots of: Observability grid, an expanded trace waterfall, the Performance two-chart layout with per-operation cards, and the Jaeger-unavailable notice.
- **UI artifacts to verify**: `/observability` (grid + expanded waterfall), `/performance` (two charts + dropdowns + per-operation cards), and the shared unavailable-notice state on both. Sidebar shows the two new entries.
- **Commit-splitting guidance**: One commit per sub-plan (foundation, observability page, performance page).
