# Plan: Statistics and Enforcement Performance Optimization — Step 3: Usage Storage

> **Status**: 🔲 Not started
> **Prerequisite**: [statistics-and-enforcement-optimization-2-enforcement-hot-path.md](statistics-and-enforcement-optimization-2-enforcement-hot-path.md)
> **Next**: [statistics-and-enforcement-optimization-4-statistics-read-path.md](statistics-and-enforcement-optimization-4-statistics-read-path.md)
> **Parent**: [statistics-and-enforcement-optimization-overview.md](statistics-and-enforcement-optimization-overview.md)

## TL;DR

Bound the size of persisted usage documents so longer retention does not translate into constantly rewriting ever-growing bucket lists. This step moves storage cost off the critical path by making writes append to small segments and making pruning/range reads cheaper.

## Reference Pattern

[../../ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs](../../ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs) contains the current flush, roll-up, and prune loops that must remain background-only.

In [../../ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs](../../ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs):
- Keep persistence work asynchronous and outside the request path.
- Preserve the current granularity progression from second to five-minute to hour to day.

[../../ClientManager.DataAccess/Databases/Implementations/UsageSnapshotRepository.cs](../../ClientManager.DataAccess/Databases/Implementations/UsageSnapshotRepository.cs) shows the existing deterministic ID pattern that should be extended instead of replaced with broad filtering.

In [../../ClientManager.DataAccess/Databases/Implementations/UsageSnapshotRepository.cs](../../ClientManager.DataAccess/Databases/Implementations/UsageSnapshotRepository.cs):
- Continue using deterministic IDs for direct lookups.
- Move query behavior toward constructing expected IDs/ranges instead of calling `GetAllAsync` and filtering in memory.

## Steps

### 1. Replace unbounded snapshots with bounded time segments

Introduce a segmented usage entity in `ClientManager.Shared/Models/Entities` or evolve the existing model so a single document only covers a bounded UTC segment instead of the full lifetime of a `(client, target, granularity)` pair.

One acceptable shape is:

```csharp
public static string BuildSegmentId(
    string clientId, TargetType targetType, string targetId,
    BucketGranularity granularity, DateOnly segmentStart);
```

Choose segment windows that keep document counts bounded without exploding document cardinality. A practical default is day-sized segments for second, five-minute, and hour buckets, with a coarser segment such as month-sized windows for day buckets.

### 2. Update flush, roll-up, and prune to operate on segments

Refactor `ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs` so:

- Fast flush writes only the current segment.
- Roll-up reads expired source segments, writes aggregated target segments, and trims source data without scanning unrelated buckets.
- Pruning can drop or rewrite only the expired segments that actually intersect the cutoff.

Keep the existing background-service split between the fast loop and slow loop.

### 3. Add targeted range retrieval methods to the usage repository

Extend `ClientManager.DataAccess/Databases/Interfaces/IUsageSnapshotRepository.cs` and `ClientManager.DataAccess/Databases/Implementations/UsageSnapshotRepository.cs` with methods that fetch only the segments needed for a target/client/range request. Avoid any new API that only wraps `GetAllByGranularityAsync`.

Prefer methods that accept explicit time windows and optional client filters so the statistics service can ask for exactly the needed segment IDs.

### 4. Keep rollout compatibility while data migrates

Use the Step 1 feature flags to support a safe cutover. During rollout, allow the repository to read both legacy unsegmented snapshots and new segmented snapshots until old data has either expired or been migrated.

Do not block the request path on any migration work.

## Verification

- The API project builds without errors.
- Generating sustained traffic no longer causes single `UsageSnapshots` documents/files to grow without bound.
- Storage-size measurements show materially smaller rewrite cost per flush, especially for second and five-minute retention.
- Historical queries across recent and older windows still return the same totals as the pre-segmentation model for the same traffic sample.
- UI: Navigate to `/monitor`, switch between short and longer time ranges, and verify charts still render continuous data without gaps caused by segment boundaries.
- UI: Navigate to `/allocations`, change the time range and client filters, and verify pool charts still line up with the table data.
- UI: Navigate to `/` and verify the usage-over-time and usage-per-client sections still populate after the storage model change.