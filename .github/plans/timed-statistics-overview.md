# Plan: Timed Statistics (Historical Usage Tracking)

## Status: 🔲 Not started

## Overview

The API currently has no persistent historical usage data. The `usage-timeseries` endpoint returns all-zeros, `RequestsPerMinute` is hardcoded to 0, and OTel metrics are in-memory counters lost on restart. This plan adds persistent, bucketed time-series tracking for both **requests per client per service** and **allocations per client per resource pool**, with configurable retention from 1 week to 3 months.

The approach uses an in-memory buffer for zero-latency event recording, a background service that periodically flushes to the existing `IDocumentStore`, and multi-tier granularity rollups (5-minute → hourly → daily) to keep storage bounded. Estimated storage: ~8–10 MB for a typical deployment with 90-day retention.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [timed-statistics-1-foundation.md](timed-statistics-1-foundation.md) | New entity models, enums, DTOs, and configuration model for usage tracking |
| 2 | [timed-statistics-2-data-layer.md](timed-statistics-2-data-layer.md) | `IUsageSnapshotRepository` interface and implementation using `IDocumentStore` |
| 3 | [timed-statistics-3-collection.md](timed-statistics-3-collection.md) | `UsageBuffer` (in-memory counters) and `IUsageRecorder` service |
| 4 | [timed-statistics-4-event-integration.md](timed-statistics-4-event-integration.md) | Wire `IUsageRecorder` calls into `AccessControlService` and `ResourceAllocationService` |
| 5 | [timed-statistics-5-background-service.md](timed-statistics-5-background-service.md) | `UsagePersistenceService` background service: flush buffer, rollup, prune |
| 6 | [timed-statistics-6-api-endpoints.md](timed-statistics-6-api-endpoints.md) | New historical query endpoint + fix existing `usage-timeseries` and `global-usage` |

## Key Decisions

- **In-memory buffer, not synchronous writes** — Recording events into `ConcurrentDictionary` counters adds near-zero latency to the request path. Data is flushed to disk every 5 minutes by a background service. Worst case: up to 5 minutes of data loss on crash.
- **Multi-tier granularity** — 5-minute buckets (retained 24h), 1-hour buckets (retained 7 days), 1-day buckets (retained 90 days). This keeps storage bounded at ~546 records per client-target pair while providing high resolution for recent data and long-term trends.
- **Single `UsageSnapshot` document per metric per granularity** — Keyed as `{clientId}:{targetType}:{targetId}:{granularity}` in the `IDocumentStore`. Each document contains an array of time-bucketed counters. Avoids per-bucket documents that would require range queries the store doesn't support.
- **Two event types tracked** — `Granted` and `Denied` counts per bucket. For services this is request granted/denied; for resource pools this is acquire succeeded/denied. Keeps the model uniform.
- **Uses existing `IDocumentStore` abstraction** — No new database dependency. Works with JsonFile, MongoDB, and Redis backends. Stored in a `"UsageSnapshots"` collection.
- **Configuration via `appsettings.json`** — Retention periods and flush interval are configurable under a `"UsageTracking"` section, with sensible defaults that require no configuration to get started.
