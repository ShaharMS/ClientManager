# Plan: Statistics and Enforcement Performance Optimization — Step 3: Read Path

> **Status**: 🔲 Not started
> **Prerequisite**: [statistics-and-enforcement-optimization-2-snapshot-segments.md](statistics-and-enforcement-optimization-2-snapshot-segments.md)
> **Next**: None — this is the final step.
> **Parent**: [statistics-and-enforcement-optimization-overview.md](statistics-and-enforcement-optimization-overview.md)

## TL;DR

The statistics endpoints currently aggregate data by loading entire collections and filtering in memory. With segmented snapshots from Step 2 and allocation counters from Step 1, this step rewires `StatisticsService` and `StatisticsController` to use direct lookups — segment-based range retrieval for time series, counter reads for pool stats, and batched ID-based fetches for breakdowns. The 5-second `IMemoryCache` wrapper stays as-is; the goal is making cache misses cheap, not tuning cache policy.

## Reference Pattern

In [../../ClientManager.Api/Services/StatisticsService.cs](../../ClientManager.Api/Services/StatisticsService.cs):
- `ComputeGlobalUsageStatsAsync` calls `GetActiveCountsByPoolAsync` (full allocation scan) and `GetAllByGranularityAsync` (full snapshot scan) — both are replaced with counters and segment lookups.
- `GetUsageTimeSeriesAsync` loops over target IDs and calls `GetByTargetAsync` per target (each is `GetAll + filter`) — replaced with `GetByTargetAndRangeAsync` from Step 2.
- `GetClientUsageBreakdownAsync` has nested loops: for each target, load all snapshots, then for each client find a match — N×M complexity. Replaced with direct `GetByClientTargetAndSegmentAsync` lookups.
- `FetchHistoricalPointsWithFallbackAsync` tries multiple granularities sequentially — this is fine as-is since it short-circuits on first data found, and each call is now a fast segment lookup.

In [../../ClientManager.DataAccess/Databases/Implementations/ResourceAllocationRepository.cs](../../ClientManager.DataAccess/Databases/Implementations/ResourceAllocationRepository.cs):
- `GetActiveCountsByPoolAsync` and `GetActiveCountsByPoolAndClientAsync` are still full-scan methods used by `StatisticsService` for dashboard and client summaries.
- With Step 1's maintained counters, these can read from counter state instead of scanning.

## Steps

### 1. Replace global stats computation with counter reads and segment lookups

**Modify `StatisticsService.ComputeGlobalUsageStatsAsync`:**

Replace the `GetActiveCountsByPoolAsync` call with a loop that reads the `alloc-count:pool:{poolId}` counters added in Step 1 (via `GetActiveCountAsync` which now reads from counters). This changes a full-collection scan into N counter reads where N = number of pools (typically <20).

Replace the `GetAllByGranularityAsync(BucketGranularity.Second)` call (which loads ALL second-granularity snapshots and filters to last 60s) with:
1. Compute the segment starts covering `[now - 60s, now]` for second granularity (typically 1-2 segments).
2. Call `GetByTargetAndRangeAsync` for each service target in the segment range, or simply enumerate the known services and construct segment IDs.

Since `GetAllByGranularityAsync` is the worst offender (loads every second-granularity snapshot in the system), this is the highest-impact change for dashboard latency.

The fallback path to `FiveMinute` granularity already does the same `GetAllByGranularityAsync` — apply the same segment-based replacement.

Files to touch:
- `ClientManager.Api/Services/StatisticsService.cs` (`ComputeGlobalUsageStatsAsync`)

### 2. Replace time-series queries with segment-range retrieval

**Modify `StatisticsService.GetUsageTimeSeriesAsync`:**

Replace the per-target `_usageSnapshotRepository.GetByTargetAsync(targetId, targetType, effectiveGranularity)` call with `GetByTargetAndRangeAsync(targetId, targetType, effectiveGranularity, effectiveFrom, effectiveTo)` from Step 2.

This change means:
- Only the segments overlapping the requested time range are loaded, not the entire history.
- The per-target loop still exists (we need data per target), but each iteration does bounded segment lookups instead of a full collection scan.

Files to touch:
- `ClientManager.Api/Services/StatisticsService.cs` (`GetUsageTimeSeriesAsync`)

### 3. Replace client breakdown queries with direct ID lookups

**Modify `StatisticsService.GetClientUsageBreakdownAsync`:**

The current pattern is:
```
for each target:
  snapshots = GetByTargetAsync(target, granularity)  // GetAll + filter
  for each client:
    snapshot = snapshots.FirstOrDefault(s => s.ClientId == client.Id)  // O(n)
```

Replace with:
```
for each target:
  for each client:
    snapshot = GetByClientAndTargetAsync(client, target, granularity)  // Direct ID lookup!
```

`GetByClientAndTargetAsync` already exists and does a direct `GetByIdAsync` using the constructed ID. For the segmented model, update this to `GetByClientTargetAndSegmentAsync` with the segment covering the relevant time window.

The granularity fallback loop (try multiple granularities until data is found) stays as-is — now each attempt inside the loop is a direct lookup rather than a scan.

Files to touch:
- `ClientManager.Api/Services/StatisticsService.cs` (`GetClientUsageBreakdownAsync`)

### 4. Replace historical usage queries with segment retrieval

**Modify `StatisticsService.FetchHistoricalPointsWithFallbackAsync`:**

The current `GetByTargetAsync` and `GetByClientAndTargetAsync` calls become their segment-aware equivalents:
- When `clientId` is specified: compute the segment starts covering `[from, to]`, call `GetByClientTargetAndSegmentAsync` for each, and merge their bucket lists.
- When `clientId` is null: call `GetByTargetAndRangeAsync` (implemented in Step 2) which handles the segment enumeration internally.

Files to touch:
- `ClientManager.Api/Services/StatisticsService.cs` (`FetchHistoricalPointsWithFallbackAsync`)

### 5. Replace client summaries allocation scan with counter reads

**Modify `StatisticsService.ComputeClientSummariesAsync`:**

Replace `GetActiveCountsByPoolAndClientAsync` (full allocation scan + GroupBy) with a loop over each client's configured pools, reading `alloc-count:client:{poolId}:{clientId}` counters via `GetActiveCountByClientAsync` (which now reads counters per Step 1).

This changes one large scan into `clients × pools-per-client` counter reads — typically under 100 total counter lookups vs. deserializing thousands of allocation documents.

Files to touch:
- `ClientManager.Api/Services/StatisticsService.cs` (`ComputeClientSummariesAsync`)

### 6. Document the performance rationale on each changed method

Every method modified in `StatisticsService` in this step must have its XML doc comment updated to explain what changed and why:

- `ComputeGlobalUsageStatsAsync`: Explain that allocation counts now come from maintained atomic counters (Step 1) and usage data comes from segment-range lookups (Step 2) instead of full-collection scans.
- `GetUsageTimeSeriesAsync`: Explain that `GetByTargetAndRangeAsync` fetches only the segments overlapping the requested time range, not the entire snapshot collection.
- `GetClientUsageBreakdownAsync`: Explain that direct `GetByClientTargetAndSegmentAsync` lookups replace the prior N×M nested-loop pattern of loading all snapshots and iterating all clients.
- `FetchHistoricalPointsWithFallbackAsync`: Note that the granularity fallback logic is unchanged but each attempt is now a bounded segment lookup.
- `ComputeClientSummariesAsync`: Explain that individual counter reads replace the prior full-allocation-collection scan with `GroupBy`.

## Verification

- The API and Admin UI projects compile without errors.
- Dashboard, monitor, and allocation endpoints return the same totals and time-series data as before for a known traffic sample.
- Cache misses on the statistics endpoints resolve noticeably faster (no full-collection scans).
- The granularity fallback logic in `GetClientUsageBreakdownAsync` and `GetHistoricalUsageAsync` still finds data correctly when the preferred granularity has no buckets.
- **UI: Navigate to `/` — verify stat cards (RPM, pool acquisition %) load quickly and show correct current values. Verify the usage chart and client overview table render with data.**
- **UI: Navigate to `/monitor` — select a service, change the time range from 5 minutes to 1 hour to 24 hours. Verify the chart shows continuous data at each range, the client breakdown table updates, and no error banners appear. Wait for at least one polling refresh.**
- **UI: Navigate to `/allocations` — select a pool, change the time range, and select/deselect clients. Verify the allocation chart, client detail grid, and all-pools grid stay consistent. Counts should match what the API returns in Swagger.**
- **UI: Take a screenshot of `/monitor` and `/allocations` with a 1-hour time range to confirm no layout regression, missing data, or empty-state bugs.**
