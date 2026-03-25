# Plan: Statistics and Enforcement Performance Optimization

## Status: 🔲 Not started

## Overview

The current statistics and enforcement paths are correct but increasingly expensive as traffic and retention grow. The request and allocation check paths still perform repeated repository lookups, global-limit lookups rely on collection scans, and resource allocation counts are derived by scanning all allocations. On the statistics side, usage persistence keeps appending to long-lived snapshot documents, and read-side queries aggregate by loading broad collections and filtering in memory.

This plan keeps the existing API behavior and rate-limit semantics intact while shifting the system toward bounded storage units, targeted lookups, and materialized read models. It follows patterns already present in the codebase: a lock-free in-memory hot path in the usage buffer, short-lived in-process caching in the statistics service, and provider-specific persistence implementations behind a common abstraction.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [statistics-and-enforcement-optimization-1-baselines.md](.github/plans/statistics-and-enforcement-optimization-1-baselines.md) | Add metrics, feature flags, and repeatable performance baselines before changing behavior. |
| 2 | [statistics-and-enforcement-optimization-2-enforcement-hot-path.md](.github/plans/statistics-and-enforcement-optimization-2-enforcement-hot-path.md) | Remove duplicated lookups and scan-based counters from the service and allocation check paths. |
| 3 | [statistics-and-enforcement-optimization-3-usage-storage.md](.github/plans/statistics-and-enforcement-optimization-3-usage-storage.md) | Replace unbounded usage snapshot growth with bounded segments and targeted range retrieval. |
| 4 | [statistics-and-enforcement-optimization-4-statistics-read-path.md](.github/plans/statistics-and-enforcement-optimization-4-statistics-read-path.md) | Build faster read-side aggregation paths for dashboard, monitor, and allocation views. |

## Key Decisions

- **Preserve semantics** — Keep access, quota, and rate-limit behavior unchanged; optimize lookup patterns and state layout instead of changing policy rules.
- **Treat service checks and allocation checks as one hot path** — Optimize both `AccessControlService` and `ResourceAllocationService`, because both are part of immediate blocking behavior.
- **Prefer deterministic IDs and bounded segments** — Avoid broad collection scans by making usage data fetchable via known IDs and small segment windows.
- **Benchmark before switching storage behavior** — Add metrics and rollout flags first so larger changes can be compared and reverted safely.