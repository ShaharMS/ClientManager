# Plan: Restore Statistics History Continuity — Step 1: Storage History

> **Status**: ✅ Completed
> **Prerequisite**: None — this is the first step.
> **Next**: [statistics-history-continuity-2-ui-chart-consumers.md](statistics-history-continuity-2-ui-chart-consumers.md)
> **Parent**: [statistics-history-continuity-overview.md](statistics-history-continuity-overview.md)

## TL;DR

Repair the storage-owned statistics layer so one request can span rolled-up history and fresh live buckets without dropping the last few minutes or showing stale overview numbers. The implementing agent should treat this as a server-side data continuity fix first, because the dashboard, monitor, and allocations pages all reuse this pipeline.

## Reference Pattern

In [../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs](../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs):
- Keep statistics composition inside the storage-owning host and continue aggregating per-target and per-client buckets there instead of pushing history stitching into controllers or the UI.
- Reuse the existing cache-key structure, target filtering, and per-timestamp aggregation patterns while replacing whole-query fallback behavior.

In [../../ClientManager.StorageApi/Services/Implementations/UsageTracking/UsagePersistenceService.Rollup.cs](../../ClientManager.StorageApi/Services/Implementations/UsageTracking/UsagePersistenceService.Rollup.cs):
- Preserve the current rollup/prune model, timestamp rounding helpers, and bucket merge behavior.
- Keep all bucket-boundary math close to the usage persistence code rather than duplicating ad hoc timestamp rules in multiple services.

In [../../ClientManager.StorageApi/Services/Implementations/StorageReadCache.cs](../../ClientManager.StorageApi/Services/Implementations/StorageReadCache.cs):
- Use explicit statistics invalidation when usage snapshots mutate.
- Keep statistics invalidation scoped separately from catalog invalidation.

## Steps

### 1. Replace whole-query granularity fallback with stitched history coverage

Edit [../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs](../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs) so `GetHistoricalUsageAsync` no longer stops at the first granularity that returns any rows. Query the requested granularity for the stable historical portion of the window, then fill uncovered ranges with finer fallback granularities from `GetGranularityFallbackOrder` without double-counting overlapping timestamps.

Apply the same continuity logic anywhere the service still assumes one granularity covers the whole request, especially `GetClientUsageBreakdownAsync`. If a shared helper is introduced, keep it private to `StatisticsService` unless another storage-side statistics method genuinely reuses it.

```csharp
private async Task<(List<HistoricalUsagePoint> Points, BucketGranularity ActualGranularity)> GetContinuousHistoryAsync(
    string targetId,
    TargetType targetType,
    string? clientId,
    DateTime from,
    DateTime to,
    BucketGranularity requested,
    CancellationToken cancellationToken)
```

### 2. Close the recent-summary blind spot

Update [../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs](../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs) so `ComputeGlobalUsageStatsAsync` and the recent client/service breakdown paths do not go empty just because the latest five-minute bucket has not rolled up yet. Prefer live second buckets when they exist, but bridge the gap to older persisted data deliberately instead of choosing one source and ignoring the other.

Do not leave `GetUsageTimeSeriesAsync` on the old semantics if you extract a reusable continuity helper. Even if the Admin UI is currently biased toward `historical-usage`, future readers should not inherit a stale endpoint contract that still drops data across rollup boundaries.

### 3. Invalidate statistics cache when usage snapshots change

Edit [../../ClientManager.StorageApi/Services/Implementations/UsageTracking/UsagePersistenceService.cs](../../ClientManager.StorageApi/Services/Implementations/UsageTracking/UsagePersistenceService.cs) and any related constructor wiring so successful buffer flushes, rollups, and pruning passes invalidate statistics cache entries through `IStorageReadCache`. Keep invalidation coarse at the statistics scope; do not churn catalog cache entries on every usage write.

Review [../../ClientManager.StorageApi/appsettings.json](../../ClientManager.StorageApi/appsettings.json) and [../../ClientManager.StorageApi/appsettings.Development.json](../../ClientManager.StorageApi/appsettings.Development.json) only after the code fix is in place. The bug should not be “fixed” by relying on the 15-second development `FlushInterval` while leaving the default five-minute configuration fundamentally incorrect.

## Verification

- `dotnet build ClientManager.StorageApi/ClientManager.StorageApi.csproj`
- `dotnet build ClientManager.Api/ClientManager.Api.csproj`
- With Storage API, API, Admin UI, seeded data, and the traffic generator running for more than six minutes, call the historical statistics endpoint for a one-hour service window and verify the response contains data older than five minutes plus a recent tail near the current time.
- UI: Navigate to `/` and verify the dashboard chart keeps extending beyond five minutes under live traffic instead of flattening to the latest short window.
- UI: Hard refresh or close and reopen `/`, then verify the chart and requests-per-minute card repopulate immediately from persisted data rather than waiting for several polls.
- UI: Navigate to `/monitor` and `/allocations` and verify their recent counters stay populated, because they share the same repaired storage-side history path.