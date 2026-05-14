# Plan: Hot Path Performance Observability — Step 3: Storage Counters

> **Status**: ✅ Completed
> **Prerequisite**: [hot-path-performance-observability-2-tracing-logs.md](hot-path-performance-observability-2-tracing-logs.md)
> **Next**: [hot-path-performance-observability-4-hot-path-logic.md](hot-path-performance-observability-4-hot-path-logic.md)
> **Parent**: [hot-path-performance-observability-overview.md](hot-path-performance-observability-overview.md)

## TL;DR

Fix the storage root cause behind hot-path 500s and reduce counter round trips. The main target is safe shared counter access when multiple storage roles point at the same JsonFile data directory, followed by batch counter APIs for rate limits and allocation counts.

## Reference Pattern

Use the existing storage abstraction rather than bypassing repositories from services.

In [IDocumentStore.cs](ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs):
- Document and counter operations already live behind one backend-neutral interface.
- Method XML docs explain backend expectations and should be updated for new batch methods.

In [JsonFileDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs):
- Reads come from an in-memory cache.
- Writes are write-through and use `AtomicWriteAsync`, which is where `_counters.json.tmp` collisions surfaced.

In [RateLimitStateDatabase.cs](ClientManager.DataAccess/Databases/Implementations/RateLimitStateDatabase.cs):
- `GetMultipleCountsAsync` and `SetMultipleCountsAsync` currently loop through single-counter store calls.
- This is the immediate caller to convert once store-level batch APIs exist.

In [ResourceAllocationDatabase.cs](ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs):
- Allocation create/release already maintains active-count counters.
- Create and release currently perform multiple store writes in sequence.

## Steps

### 1. Make JsonFile writes safe across role instances

After Step 1, there should be one JsonFile store instance per backing directory. Also harden [JsonFileDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs) itself by using a per-target-path shared lock registry or unique temp file names so external callers or future role registration changes cannot collide on the same `.tmp` path.

```csharp
var tmpPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
await File.WriteAllTextAsync(tmpPath, content, cancellationToken);
```

Ensure failed writes clean up temp files when possible.

### 2. Add batch counter methods to the store interface

Extend [IDocumentStore.cs](ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs) with batch methods for reading and setting counters. Keep the signatures simple and backend-neutral.

```csharp
Task<IReadOnlyDictionary<string, long>> GetManyCountersAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
Task SetManyCountersAsync(IReadOnlyDictionary<string, (long Value, TimeSpan Window)> entries, CancellationToken cancellationToken = default);
```

Consider increment/decrement batch methods only if allocation create/release cannot be made efficient with the first two methods.

### 3. Implement batch counters per backend

Implement the new methods in [JsonFileDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs), [LuceneDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs), [MongoDBDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs), and [RedisDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs).

JsonFile should load counters once and persist once. Redis should use a multi-key operation or pipeline. MongoDB should use a filter with `$in` and bulk writes. Lucene should avoid one commit per counter when setting multiple values.

### 4. Route rate-limit multiple reads/writes through batch APIs

Update [RateLimitStateDatabase.cs](ClientManager.DataAccess/Databases/Implementations/RateLimitStateDatabase.cs) so `GetMultipleCountsAsync` and `SetMultipleCountsAsync` delegate to the new store methods. This directly benefits [TokenBucketStrategy.cs](ClientManager.StorageApi/Services/Implementations/RateLimiting/Strategies/TokenBucketStrategy.cs), which reads/writes multiple counter keys per evaluation.

### 5. Batch allocation counter writes

Update [ResourceAllocationDatabase.cs](ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs) to reduce counter write passes during create, release, cleanup, and reconciliation. If adding increment/decrement batch methods is cleaner than multiple sequential calls, add them to [IDocumentStore.cs](ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs) in the same style and implement them in every backend.

### 6. Preserve counter correctness under concurrency

Add focused tests or a small stress verification that performs concurrent access checks and allocation acquire/release calls against JsonFile. The expected outcome is no `_counters.json.tmp` collisions, no negative counters, and no 500s from storage writes.

## Verification

- `dotnet build .\ClientManager.slnx` completes without errors.
- Existing DataAccess tests, if present, pass; otherwise add targeted tests for JsonFile batch counter reads/writes and concurrent counter updates.
- Repeated concurrent calls to `IncrementCounterAsync`, `SetManyCountersAsync`, and `DecrementCounterAsync` against the same JsonFile data directory produce no `IOException` or `UnauthorizedAccessException`.
- The benchmark no longer records 500s caused by `_counters.json.tmp` collisions.
- Redis and MongoDB implementations compile even if services are not locally available.
- **UI: Navigate to `/` under live traffic — verify dashboard counts still update and no error banner appears.**
- **UI: Navigate to `/monitor` — verify request charts continue rendering after batch counter changes.**
- **UI: Navigate to `/allocations` — verify active allocation counts are plausible after acquire/release traffic and take a screenshot.**
