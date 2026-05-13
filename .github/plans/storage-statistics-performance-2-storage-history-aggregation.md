# Plan: Storage Statistics Performance — Step 2: Storage History Aggregation

> **Status**: ✅ Completed
> **Prerequisite**: [storage-statistics-performance-1-batch-document-reads.md](storage-statistics-performance-1-batch-document-reads.md)
> **Next**: [storage-statistics-performance-3-batch-history-api-contract.md](storage-statistics-performance-3-batch-history-api-contract.md)
> **Parent**: [storage-statistics-performance-overview.md](storage-statistics-performance-overview.md)

## TL;DR

Refactor `StatisticsService` so one statistics request batches snapshot loading across targets, clients, segments, and fallback granularities. The goal is to keep long graph windows under the API timeout budget while preserving the continuity behavior that stitches rolled-up and recent buckets together.

## Reference Pattern

In [../../ClientManager.StorageApi/Services/Implementations/StatisticsService.Continuity.cs](../../ClientManager.StorageApi/Services/Implementations/StatisticsService.Continuity.cs):
- Preserve `GetContinuousHistoryAsync`, `GetContinuousBucketTotalsAsync`, and `FilterContinuityWindow` semantics.
- Keep per-timestamp aggregation in storage so UI consumers receive stable historical points.

In [../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs](../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs):
- Continue caching statistics reads through `IStorageReadCache`, but normalize cache keys so equivalent target/client selections share entries.
- Replace repeated repository calls in loops with dictionaries loaded once per request.

In [../../ClientManager.StorageApi/Services/Implementations/UsageTracking/UsagePersistenceService.Rollup.cs](../../ClientManager.StorageApi/Services/Implementations/UsageTracking/UsagePersistenceService.Rollup.cs):
- Do not change rollup or retention rules for this step; only improve read efficiency.

## Steps

### 1. Normalize request inputs once

Edit [../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs](../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs) so statistics methods materialize and normalize target IDs and client IDs at the top of each method. Sort IDs before building cache keys to improve cache hit rate for equivalent selections.

Use the normalized IDs throughout `GetUsageTimeSeriesAsync`, `GetClientUsageBreakdownAsync`, and `GetHistoricalUsageAsync`.

### 2. Add a request-scoped history loading helper

Edit [../../ClientManager.StorageApi/Services/Implementations/StatisticsService.Continuity.cs](../../ClientManager.StorageApi/Services/Implementations/StatisticsService.Continuity.cs) and introduce a private helper that can load all snapshots for a set of target IDs and client IDs for one granularity/range in a single usage database call per target, using the Step 1 client-filtered range method.

```csharp
private async Task<IReadOnlyList<UsageSnapshot>> LoadHistorySnapshotsAsync(
    IReadOnlyCollection<string> targetIds,
    TargetType targetType,
    IReadOnlyCollection<string>? clientIds,
    DateTime from,
    DateTime to,
    BucketGranularity granularity,
    CancellationToken cancellationToken)
```

Keep this helper private unless another storage service genuinely needs it.

### 3. Aggregate by target and client from loaded snapshots

Replace nested calls to `GetContinuousHistoryAsync` where a method already knows all requested targets or clients. Build dictionaries keyed by target ID, client ID, and timestamp from the loaded snapshots, then project the existing response records.

Apply this to:

- `GetHistoricalUsageAsync` for multi-target no-client requests.
- `GetClientUsageBreakdownAsync`, which currently loops target -> client -> history.
- `GetUsageTimeSeriesAsync`, especially the path where selected client IDs are aggregated one client at a time.
- `ComputeGlobalUsageStatsAsync`, which currently loops services and reads recent history separately per service.

Do not remove `GetContinuousHistoryAsync`; keep it as the single-target compatibility path and let it delegate to the same batched internals where practical.

### 4. Load caps and catalog data once per request

In [../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs](../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs), replace per-target cap lookups with dictionaries loaded once:

- For services, load global rate limits for the requested service IDs once and map by `TargetId`.
- For resource pools, load requested pools once and map `MaxSlots` by pool ID.
- For client names in breakdown responses, load client configurations once and map by client ID.

This keeps statistics methods from interleaving many small catalog reads with history reads.

## Verification

- `dotnet build ClientManager.StorageApi/ClientManager.StorageApi.csproj`
- `dotnet build ClientManager.Api/ClientManager.Api.csproj`
- With Storage API, API, Admin UI, seeded data, and traffic generator running, call `/api/v1/statistics/historical-usage` for all services over `Last 7 days` and verify it completes successfully without opening the public API storage circuit.
- Inspect the Storage API log and confirm long graph reads no longer show multi-second `historical-usage` or `client-usage-breakdown` durations under normal seeded-data load.
- UI: Navigate to `/`, select `Last 7 days` and `Last 90 days`, and verify the chart loads without replacing the page with `Unable to load data`.
- UI: While the dashboard chart is loading, confirm the traffic generator continues receiving mostly `2xx`/expected business responses rather than a burst of storage `503` responses.