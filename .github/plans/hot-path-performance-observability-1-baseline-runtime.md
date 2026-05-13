# Plan: Hot Path Performance Observability — Step 1: Baseline Runtime

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [hot-path-performance-observability-2-tracing-logs.md](hot-path-performance-observability-2-tracing-logs.md)
> **Parent**: [hot-path-performance-observability-overview.md](hot-path-performance-observability-overview.md)

## TL;DR

Make the current checkout launchable from source and make the benchmark scripts deterministic. This step turns the provisional speed test into a repeatable baseline that later storage and logic changes can compare against.

## Reference Pattern

Use the existing local runtime and script patterns rather than introducing a new load tool.

In [DocumentStoreFactory.cs](ClientManager.StorageApi/Utils/Extensions/DocumentStoreFactory.cs):
- Provider-specific store construction already belongs behind `CreateDocumentStore`.
- MongoDB and Redis clients are cached by connection string, which is the pattern to mirror for local stores by path.

In [performance_baseline.py](_scripts/performance_baseline.py):
- The script already emits structured JSON summaries with `hot_path_summary` and `runtime_unexpected_failures`.
- The action loop should remain deterministic by seed.

In [traffic_generator.py](_scripts/traffic_generator.py):
- `--interval` is already the accepted way to drive continuous live traffic.
- It reports aggregate req/min and operation counts that are useful context beside the deterministic baseline.

## Steps

### 1. Restore clean StorageApi builds

Fix the constructor mismatch between [DocumentStoreFactory.cs](ClientManager.StorageApi/Utils/Extensions/DocumentStoreFactory.cs) and [LuceneDocumentStore.cs](ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs). Prefer adding an index-directory constructor while keeping the parameterless constructor for tests/local in-memory use.

```csharp
public LuceneDocumentStore(string indexDirectory) : this(FSDirectory.Open(indexDirectory))
{
}
```

Keep the implementation under 200 lines if it grows; extract small helpers if adding a shared private constructor makes the file too dense.

### 2. Reuse local store instances per backing path

Update [StorageProviderRegistrationExtensions.cs](ClientManager.StorageApi/Utils/Extensions/StorageProviderRegistrationExtensions.cs) and [DocumentStoreFactory.cs](ClientManager.StorageApi/Utils/Extensions/DocumentStoreFactory.cs) so JsonFile and Lucene stores are cached by resolved absolute path, similar to the existing MongoDB and Redis caches. The same configured data/index path should produce the same singleton [IDocumentStore.cs](ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs) instance across roles.

### 3. Fix the benchmark empty-graph branch

Update [performance_baseline.py](_scripts/performance_baseline.py) so an `acquire` action for an actor without a preferred pool does not fall through to `graph_scenarios[graph_index % len(graph_scenarios)]` when graph reads are disabled. Either reroute to access/dashboard/monitor deterministically or record a skipped action that does not count as a runtime request.

```python
elif action == "acquire" and not actor["preferred_pool"]:
    action = "access"
```

Keep output schema backward-compatible so existing artifact comparison still works.

### 4. Add explicit benchmark artifact naming

Add an optional output argument to [performance_baseline.py](_scripts/performance_baseline.py) so the executing agent can write `before` and `after` JSON artifacts without shell redirection. Keep default behavior as stdout.

### 5. Recreate the baseline from source

Start StorageApi, Api, and AdminUI from the rebuilt checkout using the repo-root data directory for JsonFile. Seed through [seed_data.py](_scripts/seed_data.py), start [traffic_generator.py](_scripts/traffic_generator.py) with `--interval 0.2`, then run [performance_baseline.py](_scripts/performance_baseline.py) for 60 seconds. Save the clean artifact beside [hot-path-performance-baseline-provisional.json](.github/plans/hot-path-performance-baseline-provisional.json) with a clear `before` name.

## Verification

- `dotnet build .\ClientManager.slnx` completes without errors.
- StorageApi starts from source on `http://localhost:5063` with `Persistence__DefaultJsonFile__DataDirectory` set to the absolute repo-root `data` directory.
- Api starts from source on `http://localhost:5062` and can call StorageApi.
- AdminUI starts from source on `http://localhost:5100`.
- [seed_data.py](_scripts/seed_data.py) completes without creating duplicate records.
- [traffic_generator.py](_scripts/traffic_generator.py) runs with `--interval 0.2` and reports live traffic.
- [performance_baseline.py](_scripts/performance_baseline.py) runs without requiring `--include-graph-reads` and emits nonzero access/acquire/release counts.
- **UI: Navigate to `/` — verify the dashboard renders summary cards without error banners.**
- **UI: Navigate to `/monitor` — verify charts or empty states render without console-visible failures and take a screenshot.**
- **UI: Navigate to `/allocations` — verify allocation data renders and no layout breaks appear after traffic generation.**
