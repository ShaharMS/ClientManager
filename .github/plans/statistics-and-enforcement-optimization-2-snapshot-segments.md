# Plan: Statistics and Enforcement Performance Optimization — Step 2: Snapshot Segments

> **Status**: 🔲 Not started
> **Prerequisite**: [statistics-and-enforcement-optimization-1-hot-path.md](statistics-and-enforcement-optimization-1-hot-path.md)
> **Next**: [statistics-and-enforcement-optimization-3-read-path.md](statistics-and-enforcement-optimization-3-read-path.md)
> **Parent**: [statistics-and-enforcement-optimization-overview.md](statistics-and-enforcement-optimization-overview.md)

## TL;DR

Usage snapshots are currently one document per (client, target, granularity) with an ever-growing `Buckets` list. Every flush reads the entire document, appends a bucket, and rewrites the whole thing. This blocks increasing retention because documents become arbitrarily large. This step splits snapshots into bounded time-segment documents so flush writes only the current small segment, prune can drop whole documents, and retention can be extended without storage growth per document.

## Reference Pattern

In [../../ClientManager.DataAccess/Databases/Implementations/UsageSnapshotRepository.cs](../../ClientManager.DataAccess/Databases/Implementations/UsageSnapshotRepository.cs):
- `BuildId` constructs deterministic IDs as `"{clientId}:{targetType}:{targetId}:{granularity}"`.
- The segment approach extends this pattern with a time suffix — same deterministic principle, just scoped to a time window.
- `GetByClientAndTargetAsync` already does a direct `GetAsync` by constructed ID — this is the lookup pattern to follow for segment retrieval.

In [../../ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs](../../ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs):
- `FlushBufferAsync` groups by (client, target, granularity), builds the ID, loads the snapshot, appends a bucket, and upserts.
- `RollUpAsync` calls `GetAllByGranularityAsync` (full collection scan), iterates all snapshots at the source granularity.
- `PruneExpiredAsync` also calls `GetAllByGranularityAsync` per granularity.
- Both rollup and prune rewrite to the same monolithic document — with segments, they operate on small, bounded documents.

## Steps

### 1. Add a segment suffix to the snapshot ID scheme

**Update `UsageSnapshotRepository.BuildId`** and add a new `BuildSegmentId` method that appends a segment start timestamp:

```csharp
public static string BuildSegmentId(
    string clientId, TargetType targetType, string targetId,
    BucketGranularity granularity, DateTime segmentStart)
    => $"{clientId}:{targetType}:{targetId}:{granularity}:{segmentStart:yyyyMMddHH}";
```

Segment window sizes per granularity (chosen to keep bucket counts bounded per document):
- **Second** granularity → 1-hour segments (max ~3600 buckets, but with 3-min retention typically ~180 relevant)
- **FiveMinute** granularity → 1-day segments (max 288 buckets)
- **Hour** granularity → 1-week segments (max 168 buckets)
- **Day** granularity → 1-month segments (max 31 buckets)

Add a helper that computes the segment start for a given timestamp and granularity:

```csharp
public static DateTime GetSegmentStart(DateTime timestamp, BucketGranularity granularity)
```

And a helper that enumerates segment starts covering a time range:

```csharp
public static IEnumerable<DateTime> EnumerateSegmentStarts(
    DateTime from, DateTime to, BucketGranularity granularity)
```

**Add a `SegmentStart` property** to `UsageSnapshot` (nullable for backward compatibility during the transition while old data expires):

```csharp
public DateTime? SegmentStart { get; init; }
```

**Important:** The `GetSegmentStart` and `EnumerateSegmentStarts` helpers are static utility methods. If they are substantial enough to warrant their own file, place them in a dedicated `UsageSegmentHelper.cs` in the repository implementation folder — do not add them as nested types inside the repository class.

Files to touch:
- `ClientManager.DataAccess/Databases/Implementations/UsageSnapshotRepository.cs`
- `ClientManager.Shared/Models/Entities/UsageSnapshot.cs`

### 2. Update flush to write only the current segment

**Modify `UsagePersistenceService.FlushBufferAsync`** so instead of using `BuildId` (one doc per client-target-granularity), it uses `BuildSegmentId` with the current segment start. The flush now:

1. Computes `segmentStart = GetSegmentStart(bucketTimestamp, granularity)`.
2. Builds the segment ID via `BuildSegmentId(...)`.
3. Loads only that one small segment document via `GetByIdAsync(segmentId)`.
4. Appends the bucket and upserts just that segment.

The document never grows beyond one segment window's worth of buckets.

Files to touch:
- `ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs` (`FlushBufferAsync` method)

### 3. Update rollup to operate on segment documents

**Modify `UsagePersistenceService.RollUpAsync`** to replace the `GetAllByGranularityAsync` scan with targeted segment retrieval:

1. Compute which source segments contain data older than the age threshold. Use `EnumerateSegmentStarts` for the time range `[now - sourceRetention, now - ageThreshold]`.
2. For each source segment start, construct the segment IDs for all known (client, target) pairs and fetch them via `GetByIdAsync`.
3. Roll up their old buckets into the corresponding target-granularity segment document.
4. Rewrite the source segment without the rolled-up buckets (or delete it if fully consumed).

**Challenge: enumerating (client, target) pairs.** The rollup loop needs to know which combinations exist. Two options:
- **Option A**: Maintain a small index document per granularity listing active (client, target) pairs. Update it during flush.
- **Option B**: Keep `GetAllByGranularityAsync` for the rollup path only (background, not hot path). It's called every 5 minutes and is acceptable there.

**Use Option B** initially — the rollup path is background-only and runs every 5 minutes. The priority is keeping flush and reads fast. The `GetAllByGranularityAsync` call in rollup can be upgraded to an index-based approach later if the collection grows very large.

However, `GetAllByGranularityAsync` now needs to return segmented documents. Since the granularity is embedded in the ID, the existing `Where(s.Granularity == granularity)` filter still works. No change needed to that method's contract.

Files to touch:
- `ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs` (`RollUpAsync` method)

### 4. Update prune to drop whole segments

**Modify `UsagePersistenceService.PruneExpiredAsync`** (via `PruneGranularityAsync`):

1. For segments whose entire window is before the cutoff, delete the whole document via `_store.DeleteAsync` — no need to read, filter buckets, and rewrite.
2. For the boundary segment that partially overlaps the cutoff, filter its buckets and rewrite (existing logic, just scoped to one small document).

Add a `DeleteAsync(string id)` method to `IUsageSnapshotRepository` that delegates to `_store.DeleteAsync`. The interface currently only has `UpsertAsync` and read methods.

Files to touch:
- `ClientManager.DataAccess/Databases/Interfaces/IUsageSnapshotRepository.cs` (add `DeleteAsync`)
- `ClientManager.DataAccess/Databases/Implementations/UsageSnapshotRepository.cs` (implement `DeleteAsync`)
- `ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs` (`PruneExpiredAsync` / `PruneGranularityAsync`)

### 5. Add segment-aware retrieval methods to the repository

**Add to `IUsageSnapshotRepository`:**

```csharp
Task<IReadOnlyList<UsageSnapshot>> GetByTargetAndRangeAsync(
    string targetId, TargetType targetType, BucketGranularity granularity,
    DateTime from, DateTime to, CancellationToken ct);

Task<UsageSnapshot?> GetByClientTargetAndSegmentAsync(
    string clientId, string targetId, TargetType targetType,
    BucketGranularity granularity, DateTime segmentStart, CancellationToken ct);
```

**Implement `GetByTargetAndRangeAsync`** by:
1. Computing the segment starts that cover `[from, to]` via `EnumerateSegmentStarts`.
2. For each segment start, fetching all client snapshots within that segment by constructing IDs. Since we don't know which clients have data in a segment, this method falls back to loading all client configs and constructing IDs for each. This is still bounded (number of clients × number of segments) and avoids scanning the entire collection.

**Implement `GetByClientTargetAndSegmentAsync`** as a direct `GetByIdAsync` using `BuildSegmentId`.

**Return types:** These new methods return `IReadOnlyList<UsageSnapshot>` or `UsageSnapshot?`. Do not construct a `List<T>` and cast to `IReadOnlyList<T>` — collect results into a `List<T>` and return it directly (the method signature should be `List<UsageSnapshot>` if consumers need to add to it, or keep `IReadOnlyList<UsageSnapshot>` if the return comes straight from LINQ `.ToList()` on an already-`IReadOnlyList` source).

**Document the performance rationale** in XML doc comments on each new method. Example: `GetByTargetAndRangeAsync` should explain that it avoids loading the entire snapshot collection by constructing segment IDs for the requested range and fetching only those documents.

Files to touch:
- `ClientManager.DataAccess/Databases/Interfaces/IUsageSnapshotRepository.cs`
- `ClientManager.DataAccess/Databases/Implementations/UsageSnapshotRepository.cs`

## Verification

- The API project compiles without errors.
- Running sustained traffic does not cause any single usage snapshot document to contain more buckets than the segment window allows (e.g., no second-granularity document has buckets spanning more than 1 hour).
- Historical queries across segment boundaries return continuous data — no gaps at segment edges.
- Rollup still correctly aggregates seconds → 5-minute → hour → day across segment boundaries.
- Prune drops whole-segment documents when fully expired, reducing storage I/O vs. the old rewrite-every-document approach.
- **UI: Navigate to `/monitor`, select a service, and switch between short (5 min) and longer (1 hour, 24 hour) time ranges — verify charts render continuous data without gaps at segment boundaries.**
- **UI: Navigate to `/allocations`, select a pool, and switch time ranges — verify the allocation charts show consistent data across segment edges.**
- **UI: Navigate to `/` — verify dashboard stat cards still populate correctly (RPM computed from recent second-granularity segments).**
- **UI: Take a screenshot of `/monitor` with a 1-hour time range to confirm no visual regression or data gaps.**
