# Plan: Code Slim-Down — Step 2: Data Access Layer

> **Status**: ✅ Completed
> **Prerequisite**: [code-slimdown-1-shared-foundation.md](code-slimdown-1-shared-foundation.md)
> **Next**: [code-slimdown-2b-storage-bindings.md](code-slimdown-2b-storage-bindings.md)
> **Parent**: [code-slimdown-overview.md](code-slimdown-overview.md)

## TL;DR

Remove pass-through abstractions and copy-paste query building in `ClientManager.DataAccess`: eliminate or thin the counter wrapper and the generic `EntityRepository<T>` indirection where it adds no behavior, switch `GlobalRateLimitDatabase` from inheritance to composition, and extract shared helpers for the repeated sub-document mutation and `DocumentQuery` builder patterns.

## Iteration Bootstrap

- **Iteration slug**: `code-slimdown`
- **Required evidence**: `dotnet build ClientManager.DataAccess` clean; `ClientManager.DataAccess.Tests` all pass (this is the safety net for behavior parity); `git diff --stat` net deletions.
- **UI artifacts to verify**: After a full stack run, exercise CRUD on a list page (e.g., create/edit a Service at `/services`) and confirm persistence still works end-to-end.
- **Commit-splitting guidance**: Separate commits for (a) wrapper removal/DI rewiring, (b) repository thinning, (c) shared helper extraction.

## Reference Pattern

In [ClientManager.DataAccess/Repositories/Implementations/EntityRepository.cs](ClientManager.DataAccess/Repositories/Implementations/EntityRepository.cs):
- Every method is a one-line delegate to `IDocumentStore` — this is the pass-through shape to remove or inline.

In [ClientManager.DataAccess/Databases/Implementations/ClientConfigurationDatabase.cs](ClientManager.DataAccess/Databases/Implementations/ClientConfigurationDatabase.cs):
- The service-settings and resource-pool-settings mutation blocks are mirror images (load → null-check → copy dictionary → modify → save) — the duplication to fold into one generic helper.

In [ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs](ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs):
- `GetByTargetAsync` / `GetByTargetTypeAsync` / `GetByClientAndTargetAsync` build `DocumentQuery` with the same scaffolding — the duplication to fold into one query-builder helper.

## Steps

### 1. Remove or collapse the counter pass-through wrapper

Inspect [ClientManager.DataAccess/Databases/Implementations/RateLimitStateDatabase.cs](ClientManager.DataAccess/Databases/Implementations/RateLimitStateDatabase.cs). If every method is an identity delegate to `IDocumentStore` counter methods, remove the wrapper and inject the store (or the counter interface) directly into its callers. If a thin interface is still needed for DI seams, keep the interface but make the implementation a primary-constructor one-liner per method. Update DI registration in the StorageApi composition root accordingly.

### 2. Thin `EntityRepository<T>`

In [ClientManager.DataAccess/Repositories/Implementations/EntityRepository.cs](ClientManager.DataAccess/Repositories/Implementations/EntityRepository.cs), confirm whether `IEntityRepository<T>` is a genuine polymorphic seam (multiple implementations / mocked in tests) or a single pass-through. If pass-through only, inline its usage into callers and delete the pair. If it must stay, convert to a primary constructor and expression-bodied one-liners. Use find-all-references before deleting.

### 3. Convert `GlobalRateLimitDatabase` from inheritance to composition

In [ClientManager.DataAccess/Databases/Implementations/GlobalRateLimitDatabase.cs](ClientManager.DataAccess/Databases/Implementations/GlobalRateLimitDatabase.cs), stop inheriting `EntityRepository<GlobalRateLimit>` (which duplicates the `_store` field). Compose `IDocumentStore` via primary constructor and extract the repeated target-query construction into a single private builder:

```csharp
private static DocumentQuery BuildTargetQuery(string id, TargetType type) => /* one expression */;
```

### 4. Extract a sub-document mutation helper in `ClientConfigurationDatabase`

In [ClientManager.DataAccess/Databases/Implementations/ClientConfigurationDatabase.cs](ClientManager.DataAccess/Databases/Implementations/ClientConfigurationDatabase.cs), add one generic private helper that loads the config, null-checks, applies a caller-supplied mutation to the target dictionary, and saves. Rewrite the service-settings and resource-pool-settings set/remove methods to call it.

```csharp
private async Task MutateAsync<TValue>(string clientId, Func<ClientConfiguration, IDictionary<string, TValue>> select, Action<IDictionary<string, TValue>> mutate, CancellationToken ct);
```

### 5. Extract a query-builder helper in `UsageSnapshotDatabase`

In [ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs](ClientManager.DataAccess/Databases/Implementations/UsageSnapshotDatabase.cs), add one private `DocumentQuery` builder taking optional `clientId` / `targetId` / `targetType` / time-range parameters, and reduce each public method to build-query + search.

### 6. Extract counter-delta accumulation in `ResourceAllocationDatabase`

In [ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs](ClientManager.DataAccess/Databases/Implementations/ResourceAllocationDatabase.cs), pull the nested reconcile loop body into a named private helper to flatten nesting (max 2 levels) and remove duplicated key construction.

## Verification

- `dotnet build ClientManager.DataAccess` compiles cleanly.
- `ClientManager.DataAccess.Tests` run green — primary behavior-parity gate for this step.
- A sample end-to-end check: via the running StorageApi, perform a set-service-settings then get-service-settings on a client and confirm the value round-trips (the mutation-helper refactor path).
- `git diff --stat` shows net deletions.
- **UI: With the full stack running, create a new Service and a new Resource Pool through the AdminUI (`/services`, `/resourcepools`), edit one, then delete it. Verify each operation succeeds with no error toast and the grid refreshes — exercises the repository/database changes through the real call path.**
- **UI: Open `/ratelimits` and confirm global rate-limit rows still load (exercises `GlobalRateLimitDatabase` composition change).**
