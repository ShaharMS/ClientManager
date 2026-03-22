# Plan: Multi-Platform Metrics Export

## Status: 🔲 Not started

## Overview

Currently, the MetricsController exposes a single `/metrics` endpoint that returns Prometheus exposition format. This plan restructures the controller to support multiple metrics platforms with dedicated routes:

- `/prometheus` — Prometheus exposition format (text/plain)
- `/grafana` — OpenMetrics JSON format

The existing `/metrics` endpoint will be removed in favor of explicit platform routes. This gives observability teams flexibility to choose their preferred monitoring stack while keeping the underlying metrics data consistent across both formats.

The implementation follows the existing `PrometheusExportService` pattern, adding a parallel `GrafanaExportService` that renders the same metrics as JSON.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [metrics-export-1-interfaces.md](metrics-export-1-interfaces.md) | Define abstraction layer with format-agnostic interface |
| 2 | [metrics-export-2-grafana-service.md](metrics-export-2-grafana-service.md) | Implement GrafanaExportService for OpenMetrics JSON |
| 3 | [metrics-export-3-controller-routes.md](metrics-export-3-controller-routes.md) | Restructure MetricsController with platform-specific routes |

## Key Decisions

- **OpenMetrics JSON for Grafana** — Uses JSON encoding of the same metrics exposed via Prometheus. Structure mirrors Prometheus metrics with `name`, `type`, `help`, and `values` arrays.
- **Explicit platform routes over content negotiation** — Separate `/prometheus` and `/grafana` endpoints rather than a single endpoint with Accept header switching. Clearer for infrastructure configuration.
- **Remove `/metrics` endpoint** — No backwards compatibility alias; teams must update scrape configs to use `/prometheus`.
- **Shared metrics data** — Both services read from the same repositories (`IUsageSnapshotRepository`, `IEntityRepository<ResourcePool>`, `IResourceAllocationRepository`, `IStatisticsService`). No duplication of data collection logic.
