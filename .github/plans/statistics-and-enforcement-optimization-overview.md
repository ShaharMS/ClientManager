# Plan: Statistics and Enforcement Performance Optimization

## Status: 🔲 Not started

## Overview

Two problems need solving: (1) usage snapshot documents grow unbounded as retention increases, blocking the ability to store more historical graph data, and (2) the enforcement hot paths — access checks and resource acquisition — perform redundant repository loads and full-collection scans on every request.

The current access-check path loads client config **three times** per request (once in `AccessControlService`, then again in each `RateLimitService` method it calls). Global rate limit lookups call `GetAll + FirstOrDefault` on every check. Resource acquisition scans the entire allocation collection **twice** (once for per-client count, once for pool-wide count). On the storage side, each usage snapshot is a single ever-growing document per (client, target, granularity) — flush rewrites the whole document every second and prune/rollup scan all documents at every granularity.

This plan fixes both problems by: passing config forward instead of re-fetching, replacing scans with maintained counters and caches, splitting snapshots into bounded time segments, and making statistics queries use direct ID-based lookups instead of collection-wide scans.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [statistics-and-enforcement-optimization-1-hot-path.md](statistics-and-enforcement-optimization-1-hot-path.md) | Eliminate redundant config loads, collection scans, and state-store round trips from access checks and resource acquisition. |
| 2 | [statistics-and-enforcement-optimization-2-snapshot-segments.md](statistics-and-enforcement-optimization-2-snapshot-segments.md) | Split usage snapshots into bounded time-segment documents so retention can grow without unbounded document sizes. |
| 3 | [statistics-and-enforcement-optimization-3-read-path.md](statistics-and-enforcement-optimization-3-read-path.md) | Replace `GetAll + filter` patterns in statistics queries with direct segment lookups and allocation counters. |

## Key Decisions

- **No feature flags or baselines step** — Metrics already exist in `ClientManagerMetrics`. Each change is independently verifiable through the UI dashboard, monitor, and allocations pages. Feature flags add complexity for optimizations that can be tested directly.
- **Pass config forward, don't cache it** — Client config can change at any time, so scoped caching is risky. Instead, refactor `RateLimitService` to accept already-loaded config as a parameter, eliminating 2 of 3 loads per access check.
- **In-memory allocation counters via `IDocumentStore` atomic counters** — Use the existing counter API (same pattern as rate-limit state) to maintain `active-count:{poolId}` and `active-count:{poolId}:{clientId}` counters, updated on create/release/cleanup. Eliminates full-collection scans on every acquire.
- **Short-TTL `IMemoryCache` for global rate limits** — Global rate limits change rarely (admin-only edits). A 30-second memory cache in `RateLimitService` eliminates the `GetAll + FirstOrDefault` scan on every request while staying fresh enough.
- **Time-segment IDs for snapshots** — Extend the existing deterministic ID pattern with a segment suffix. Old unsegmented data expires naturally via retention; no migration logic needed.
- **Compound state store operations for TokenBucket** — Add `GetMultipleCountsAsync` and `SetMultipleCountsAsync` to `IRateLimitStateStore` to cut TokenBucket from 4 round trips to 2 per evaluation.

## Implementation Discipline

These rules apply to **all steps** and must be followed by the implementing agent:

- **No `List<T>` → `IReadOnlyList<T>` casts.** Repository methods that return `IReadOnlyList<T>` already receive that type from `IDocumentStore.GetAllAsync<T>`. New code should return `List<T>` where mutation is needed (internal), or accept the `IReadOnlyList<T>` from upstream as-is. Never construct a `List<T>` and cast it — if the method signature needs a list, just return `List<T>`. If the consumer should not mutate, return `IReadOnlyList<T>` directly from the source.
- **One type per file.** Every new `record`, `class`, or `enum` goes in its own file under the appropriate namespace folder. Do not place helper records, DTOs, or sealed classes in the same file as a service or repository. Follow the existing codebase convention where `AccessDenialReason` and `ResourceDenialReason` each live in `DenialReasons.cs` (acceptable: small related enums in one file; not acceptable: a class and its consumer in one file).
- **Metric tag keys must use an enum, not strings.** The current code uses raw string literals like `"clientId"`, `"serviceId"`, `"resourcePoolId"`, `"allocationId"`, `"reason"` in `TagList` entries. Create a `MetricTagKey` enum (or similar) in `ClientManager.Api/Services/Instrumentation/` with members for each tag key, and a `ToTagName()` extension method that returns the snake_case or camelCase string. All existing and new `TagList` entries must use the enum. This is a cross-cutting change that applies whenever metrics are touched in any step.
- **Document the _why_ behind performance changes.** XML doc comments on optimized methods must explain _what performance problem the change solves_ — not just what the method does. For example, a cached global rate limit lookup should explain that the cache avoids a `GetAll + FirstOrDefault` scan on every request. A config-forwarding parameter should explain that it eliminates a redundant repository load.
