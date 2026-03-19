# Plan: Timed Statistics — Step 2: Data Layer

> **Status**: ✅ Completed
> **Prerequisite**: [timed-statistics-1-foundation.md](timed-statistics-1-foundation.md)
> **Next**: [timed-statistics-3-collection.md](timed-statistics-3-collection.md)
> **Parent**: [timed-statistics-overview.md](timed-statistics-overview.md)

## TL;DR

Create the `IUsageSnapshotRepository` interface and its implementation that persists `UsageSnapshot` documents via the existing `IDocumentStore`. This is the data access layer for reading and writing time-series usage data.

## Reference Pattern

In [ClientManager.DataAccess/Interfaces/IResourceAllocationRepository.cs](ClientManager.DataAccess/Interfaces/IResourceAllocationRepository.cs):
- Interface with XML doc comments on every method
- Async methods with `CancellationToken` parameters
- Lives in `ClientManager.DataAccess.Interfaces` namespace

In [ClientManager.DataAccess/Implementations/ResourceAllocationRepository.cs](ClientManager.DataAccess/Implementations/ResourceAllocationRepository.cs):
- Delegates to `IDocumentStore` with a `const string Collection`
- Constructor receives `IDocumentStore`
- In-memory filtering over `GetAllAsync` results
- Lives in `ClientManager.DataAccess.Implementations` namespace

## Steps

### 1. Create `IUsageSnapshotRepository` interface

File: `ClientManager.DataAccess/Interfaces/IUsageSnapshotRepository.cs`

```csharp
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Interfaces;

/// <summary>
/// Repository for persisting and querying time-bucketed usage snapshots.
/// </summary>
public interface IUsageSnapshotRepository
{
    /// <summary>
    /// Gets a usage snapshot by its compound key.
    /// </summary>
    Task<UsageSnapshot?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all snapshots for a specific target at a specific granularity.
    /// </summary>
    Task<IReadOnlyList<UsageSnapshot>> GetByTargetAsync(
        string targetId,
        GlobalRateLimitTarget targetType,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a snapshot for a specific client-target-granularity combination.
    /// </summary>
    Task<UsageSnapshot?> GetByClientAndTargetAsync(
        string clientId,
        string targetId,
        GlobalRateLimitTarget targetType,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a usage snapshot document.
    /// </summary>
    Task UpsertAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all snapshots at a specific granularity level.
    /// </summary>
    Task<IReadOnlyList<UsageSnapshot>> GetAllByGranularityAsync(
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);
}
```

### 2. Create `UsageSnapshotRepository` implementation

File: `ClientManager.DataAccess/Implementations/UsageSnapshotRepository.cs`

```csharp
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Implementations;

/// <summary>
/// Persists usage snapshots in <see cref="IDocumentStore"/> and performs in-memory filtering for queries.
/// </summary>
public class UsageSnapshotRepository : IUsageSnapshotRepository
{
    private readonly IDocumentStore _store;
    private const string Collection = "UsageSnapshots";

    public UsageSnapshotRepository(IDocumentStore store)
    {
        _store = store;
    }

    public Task<UsageSnapshot?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _store.GetAsync<UsageSnapshot>(Collection, id, cancellationToken);

    public async Task<IReadOnlyList<UsageSnapshot>> GetByTargetAsync(
        string targetId, GlobalRateLimitTarget targetType, BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<UsageSnapshot>(Collection, cancellationToken);
        return all.Where(s =>
            s.TargetId == targetId &&
            s.TargetType == targetType &&
            s.Granularity == granularity).ToList();
    }

    public async Task<UsageSnapshot?> GetByClientAndTargetAsync(
        string clientId, string targetId, GlobalRateLimitTarget targetType,
        BucketGranularity granularity, CancellationToken cancellationToken = default)
    {
        var id = BuildId(clientId, targetType, targetId, granularity);
        return await _store.GetAsync<UsageSnapshot>(Collection, id, cancellationToken);
    }

    public Task UpsertAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default) =>
        _store.SetAsync(Collection, snapshot.Id, snapshot, cancellationToken);

    public async Task<IReadOnlyList<UsageSnapshot>> GetAllByGranularityAsync(
        BucketGranularity granularity, CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<UsageSnapshot>(Collection, cancellationToken);
        return all.Where(s => s.Granularity == granularity).ToList();
    }

    /// <summary>
    /// Builds the compound document ID used as the store key.
    /// </summary>
    public static string BuildId(
        string clientId, GlobalRateLimitTarget targetType,
        string targetId, BucketGranularity granularity) =>
        $"{clientId}:{targetType}:{targetId}:{granularity}";
}
```

### 3. Register the repository in DI

In [ClientManager.Api/Extensions/ServiceCollectionExtensions.cs](ClientManager.Api/Extensions/ServiceCollectionExtensions.cs), add to the `RegisterRepositories` method:

```csharp
services.AddSingleton<IUsageSnapshotRepository, UsageSnapshotRepository>();
```

## Verification

- Solution compiles without errors
- `IUsageSnapshotRepository` is resolvable from DI
- `UsageSnapshotRepository.BuildId` produces keys in the format `"{clientId}:{targetType}:{targetId}:{granularity}"`
- CRUD operations work against the `"UsageSnapshots"` collection in the document store
