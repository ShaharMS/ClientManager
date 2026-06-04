# Plan: Code Slim-Down — Step 5: Merged API Services Consolidation

> **Status**: ✅ Completed
> **Prerequisite**: [code-slimdown-4-storageapi-controllers-wiring.md](code-slimdown-4-storageapi-controllers-wiring.md)
> **Next**: [code-slimdown-6-api-controllers.md](code-slimdown-6-api-controllers.md)
> **Parent**: [code-slimdown-overview.md](code-slimdown-overview.md)

## TL;DR

Now that there is a single set of services in the API, apply the structural slim-downs that were duplicated across the old two-project layout only once: collapse the four near-identical catalog services into one generic base, extract the repeated telemetry try/catch/finally envelope in the access/resource/rate-limit services, inline the swarm of tiny activity-creation helpers, and simplify `ClientLookup`/settings null-check patterns.

## Iteration Bootstrap

- **Iteration slug**: `code-slimdown`
- **Required evidence**: `dotnet build ClientManager.Api` clean; access-check and acquire/release return identical responses and still emit the same trace/metric names; `git diff --stat` net deletions.
- **UI artifacts to verify**: Monitor and Allocations show live data after the telemetry refactor; CRUD reads work after the catalog-base refactor.
- **Commit-splitting guidance**: (a) generic catalog base + subclasses, (b) telemetry wrapper extraction, (c) activity-helper inlining, (d) ClientLookup/settings null-check simplification.

## Reference Pattern

In the API's relocated catalog services (`ServiceCatalogService`, `ResourcePoolCatalogService`, `GlobalRateLimitCatalogService`, `ClientConfigurationCatalogService`):
- All implement Search/GetById/Create/Update/Delete with the same cache-key, not-found-throw, and cache-invalidation shape, varying only by entity type and the not-found/conflict exception.

In the API's relocated `AccessControlService`:
- `CheckAccessAsync` wraps business logic in an Activity + Stopwatch + catch-by-exception-type + finally(record duration/log) envelope; `ResourceAllocationService.AcquireAsync`/`ReleaseAsync` and `RateLimitService` repeat the identical envelope. The inner business logic is the only part that differs.

## Steps

### 1. Introduce a generic catalog service base

Create a generic base (`GenericCatalogService<TEntity>`) implementing Search/GetById/Create/Update/Delete against an injected `DataAccess` repository/database plus caching, with hooks for the entity id selector and its not-found/conflict exception factories.

```csharp
public abstract class GenericCatalogService<TEntity>(/* repository, cache */)
{
    protected abstract string GetId(TEntity entity);
    protected abstract Exception NotFound(string id);
    // Search/GetById/Create/Update/Delete implemented once
}
```

Reduce the four catalog services to thin subclasses supplying only the type, id selector, and exception factory. Preserve all cache keys and invalidation behavior. `ClientConfigurationCatalogService`'s extra sub-document methods stay; only its CRUD core folds into the base.

### 2. Extract a telemetry wrapper helper

Add a reusable async helper that opens the Activity, starts the Stopwatch, runs the supplied operation, and handles the catch-by-type → result-label + finally(record duration/log) envelope once.

```csharp
private async Task<TResult> TraceAsync<TResult>(string activityName, Action<Activity?> tag, Func<Task<TResult>> operation, CancellationToken ct);
```

Rewrite `CheckAccessAsync`, `AcquireAsync`/`ReleaseAsync`, and the rate-limit trace path to call it with only their distinct logic and tags. **Preserve the exact activity names, tag keys, metric names, and result labels** — these are observability contracts validated by the existing observability plans in `.github/plans/`.

### 3. Inline the single-purpose activity helpers

Replace the many tiny `private async Task<T> GetXAsync(...)` methods that only start an activity, set one tag, call storage, and set a result tag with inline calls or one small `StartActivityWithTag(name, key, value)` helper. Keep nesting ≤2 levels.

### 4. Simplify `ClientLookup` and settings null-checks

Evaluate `ClientLookup<T>` (relocated from StorageApi). If it only signals "client exists" + "value", inline its retrieval into callers (load config once, null-check client, then null-check the sub-value) or keep the record but drop redundant layers — whichever yields fewer lines without obscuring not-found vs. settings-not-found. Apply a single `OrThrow<T>(this T?, Func<Exception>)` helper (or keep terse inline `??`) across the client settings services.

## Verification

- `dotnet build ClientManager.Api` compiles cleanly.
- Access-check and acquire/release return identical bodies/status to pre-refactor (compare via the running API).
- Trace/metric parity: activity names and tag keys unchanged (diff against the observability measurement notes in `.github/plans/`).
- `git diff --stat` shows net deletions (largest service-layer reduction here).
- **UI: With the full stack + traffic, open Monitor and Allocations and confirm live charts/tables update — exercises the refactored telemetry paths.**
- **UI: Load `/services`, `/resourcepools`, `/ratelimits` and confirm reads work (generic catalog base). Screenshot Monitor under load.**
