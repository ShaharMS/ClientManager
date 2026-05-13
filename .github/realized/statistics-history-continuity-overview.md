# Plan: Restore Statistics History Continuity

## Status: ✅ All steps completed

## Overview

The Admin UI currently asks for chart windows ranging from one minute to ninety days, but the storage-side statistics reader treats each request as if one bucket granularity can cover the entire window. That assumption breaks against the actual retention model: per-second buckets are short-lived, five-minute buckets are rolled up later, and the live dashboard cards use separate recent-data logic. The result matches the reported behavior: charts often appear to stop around the last few minutes, reopening the UI can make history look missing until newer polls arrive, and recent requests-per-minute values can show a blind spot around the current five-minute boundary.

The target state is a continuous history pipeline across the shared statistics surfaces. `ClientManager.StorageApi` should return contiguous data by combining persisted coarse buckets with fresher fine-grained buckets, invalidate statistics cache entries when usage snapshots change, and keep recent overview metrics aligned with the same continuity rules. `ClientManager.AdminUI` should then consume those responses through timestamp-based bucketing, so `/`, `/monitor`, and `/allocations` render the repaired history without label collisions, blank tails, or refresh-only recovery.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [statistics-history-continuity-1-storage-history.md](.github/plans/statistics-history-continuity-1-storage-history.md) | Repair the storage-side history, recent-summary, and cache invalidation logic so persisted usage data remains continuous across rollups. |
| 2 | [statistics-history-continuity-2-ui-chart-consumers.md](.github/plans/statistics-history-continuity-2-ui-chart-consumers.md) | Update the Admin UI chart pages to bucket repaired history safely and validate short-range behavior after reopen and refresh. |

## Key Decisions

- **Hybrid reads over retention inflation** — Fix the read model to stitch coarse and fine buckets instead of relying on longer second retention or dev-only timing tweaks to hide the gap.
- **Statistics invalidation on writes** — Usage flushes, rollups, and pruning must invalidate statistics cache entries explicitly; a five-second TTL alone is not a correctness guarantee for live dashboards.
- **Shared regression surface** — Dashboard, Monitor, and Allocations all rely on the same historical and recent statistics pipeline, so verification must cover all three pages even if the user first noticed the bug on `/`.
- **Do not trust dev cadence alone** — `ClientManager.StorageApi/appsettings.Development.json` uses a 15-second slow loop, but `appsettings.json` still defaults to five minutes; the fix must remain correct under both configurations.