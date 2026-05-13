# Plan: Hot Path Performance Observability

## Status: 🚧 In progress

## Overview

This plan makes the access-check and resource-allocation hot paths measurable, traceable, and faster. A provisional 60-second baseline was captured while the low-interval traffic generator ran at `--interval 0.2`; the artifact is [hot-path-performance-baseline-provisional.json](.github/plans/hot-path-performance-baseline-provisional.json). Under that load, access checks averaged 65.156 ms with 151.374 ms p95, resource acquires averaged 55.404 ms with 99.543 ms p95, and releases averaged 57.726 ms with 101.346 ms p95. The benchmark also exposed 24 runtime 500s across access/acquire/release.

The current checkout cannot be cleanly launched from source because [DocumentStoreFactory.cs](ClientManager.StorageApi/Utils/Extensions/DocumentStoreFactory.cs) constructs [LuceneDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs) with an index directory argument that the store no longer accepts. The 500s seen in the running stack trace back to concurrent JSON-file counter writes colliding on `_counters.json.tmp`, caused by multiple role-specific [JsonFileDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs) instances pointing at the same data directory with separate locks. The desired end state is a clean rebuildable stack, distributed spans and structured timing logs for every major hot-path segment, batched/role-safe counter storage, and repeatable before/after benchmark artifacts with no storage-write 500s.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [hot-path-performance-observability-1-baseline-runtime.md](.github/plans/hot-path-performance-observability-1-baseline-runtime.md) | Make the local stack buildable from source and make the benchmark harness reliable. |
| 2 | [hot-path-performance-observability-2-tracing-logs.md](.github/plans/hot-path-performance-observability-2-tracing-logs.md) | Add distributed tracing, operation histograms, and structured timing logs across Api, StorageApi, and storage calls. |
| 3 | [hot-path-performance-observability-3-storage-counters.md](.github/plans/hot-path-performance-observability-3-storage-counters.md) | Fix JSON-file counter contention and add batch counter APIs used by rate limits and allocations. |
| 4 | [hot-path-performance-observability-4-hot-path-logic.md](.github/plans/hot-path-performance-observability-4-hot-path-logic.md) | Reduce avoidable work in access checks, rate-limit evaluation, acquire, and release. |
| 5 | [hot-path-performance-observability-5-verification.md](.github/plans/hot-path-performance-observability-5-verification.md) | Rerun the low-interval load, compare artifacts, inspect traces/logs, and verify UI behavior. |

## Key Decisions

- **Baseline before optimization** — Preserve the provisional benchmark artifact and require a clean rebuilt baseline after Step 1 so improvements are measured against source, not stale running binaries.
- **Distributed traces plus existing metrics** — Keep the current Prometheus metrics endpoint and add ActivitySource/OTLP tracing so Grafana-style metrics and trace-waterfall tools are both supported.
- **StorageApi owns hot-path data work** — Keep public API controllers thin and keep performance work inside StorageApi services/data access; do not reference AdminUI from API projects.
- **Batch counters at the storage abstraction** — Add batch counter operations to [IDocumentStore.cs](ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs) instead of adding local one-off batching in services.
- **Share local store instances by backing path** — For JsonFile/Lucene role bindings that target the same path, reuse one store instance or otherwise share per-file locks so role-specific registrations cannot race each other on the same files.
- **No silent benchmark failures** — Fix [performance_baseline.py](_scripts/performance_baseline.py) so unsupported action/actor combinations are recorded or rerouted explicitly instead of falling into the graph branch.
