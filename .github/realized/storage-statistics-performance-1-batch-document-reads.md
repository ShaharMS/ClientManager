# Plan: Storage Statistics Performance — Step 1: Batch Document Reads

> **Status**: ✅ Completed
> **Prerequisite**: None — this is the first step.
> **Next**: [storage-statistics-performance-2-storage-history-aggregation.md](storage-statistics-performance-2-storage-history-aggregation.md)
> **Parent**: [storage-statistics-performance-overview.md](storage-statistics-performance-overview.md)

## TL;DR

Add a direct multi-key read path to the document-store abstraction and use it for usage snapshot segment IDs. This is the foundation for making long statistics windows cheap without scanning full collections or issuing one storage lookup at a time.

## Reference Pattern

In [../../ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs](../../ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs):
- Keep generic persistence capabilities in the lowest-level store abstraction, with XML docs explaining backend expectations.
- Preserve cancellation token flow on every storage operation.

In [../../ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs](../../ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs):
- Continue constructing deterministic usage snapshot IDs with `UsageSegmentHelper` instead of scanning all snapshots.
- Keep range queries bounded by known client IDs and segment starts.

In [../../ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs](../../ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs):
- Avoid `SearchAsync` for targeted reads because it currently calls `GetAllAsync` and filters in memory.
- Reuse the existing `_collection`, `_id`, and `_json` fields for native Lucene lookup.

## Steps

### 1. Add a multi-key read to the store interface

Edit [../../ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs](../../ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs) and add a generic `GetManyAsync` method beside `GetAsync`. Document that implementations should return only found documents and should avoid full-collection scans when the backend supports direct key lookup.

```csharp
Task<IReadOnlyList<T>> GetManyAsync<T>(
    string collection,
    IEnumerable<string> ids,
    CancellationToken cancellationToken = default) where T : class;
```

### 2. Implement direct batch reads in every store

Edit all `IDocumentStore` implementations:

- [../../ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs](../../ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs): load the collection cache once, then deserialize matching IDs from that dictionary.
- [../../ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs](../../ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs): use an `_id in ids` filter and deserialize the returned documents.
- [../../ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs](../../ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs): use a single hash multi-get for the hash provider path, and pipeline/batch JSON `DocKey` reads for the RediSearch JSON path.
- [../../ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs](../../ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs): perform one search per bounded ID chunk using `_collection` plus an ID terms query, with only one `MaybeRefreshBlocking` per chunk.

Keep the return type as a list of documents, not a dictionary, unless the implementation already has a local reason to preserve ID order internally.

### 3. Expose batch segment reads from usage snapshots

Edit [../../ClientManager.DataAccess/Databases/Interfaces/IUsageSnapshotDatabase.cs](../../ClientManager.DataAccess/Databases/Interfaces/IUsageSnapshotDatabase.cs) and [../../ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs](../../ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs) to add a usage-specific batch method that accepts already-built segment IDs.

```csharp
Task<IReadOnlyList<UsageSnapshot>> GetByIdsAsync(
    IEnumerable<string> ids,
    CancellationToken cancellationToken = default);
```

Update `GetByTargetAndRangeAsync` so it builds all needed segment IDs and calls `GetByIdsAsync` once per range instead of awaiting `_store.GetAsync` inside nested client/segment loops.

### 4. Add a client-filtered range overload

Add an overload or optional parameter on `GetByTargetAndRangeAsync` that accepts a preselected client ID set. The existing no-client-filter call can continue to fetch all clients from `IClientConfigurationDatabase`, but storage statistics code should be able to pass known clients and avoid repeated client enumeration.

## Verification

- `dotnet build ClientManager.DataAccess/ClientManager.DataAccess.csproj`
- `dotnet build ClientManager.StorageApi/ClientManager.StorageApi.csproj`
- Confirm no remaining usage snapshot range path performs `_store.GetAsync` inside both a client loop and a segment loop.
- With the Storage API running, call a storage-side `historical-usage` request for a seven-day and ninety-day range and confirm it still returns history points.
- UI: Navigate to `/`, select `Last 7 days`, and verify the dashboard does not show an error banner while the chart loads.
- UI: Navigate to `/monitor` and `/allocations` after the backend change and verify their charts still render, because all chart pages share the usage snapshot read path.