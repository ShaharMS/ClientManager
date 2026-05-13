# Review Packet

## Review Source

- Source type: @Inspect re-review approval after commit 8d3e217
- Scope: .github/plans/hot-path-performance-observability-3-storage-counters.md; re-review of RVW-001 through RVW-004 follow-up fixes on feature/hot-path-performance-observability-1-baseline-runtime
- Baseline: Original implementation baseline 18c8a67ea9633abd0e044a3eafdce29ddefc4d8d; latest reviewed follow-up commit 8d3e217
- Reviewer: @Inspect

## Review Checklist

- [x] Plan intent reviewed
- [x] Verification claims checked
- [x] Repository conventions checked
- [x] Shared package boundaries checked
- [ ] Naming and structure checked
- [ ] Nesting and complexity checked
- [x] Risks and regressions checked

## Findings

| Finding ID | Severity | File | Concern | Required action | Evidence |
|------------|----------|------|---------|-----------------|----------|
| RVW-001 | MAJOR | ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs | MongoDB batch decrement can persist or expose negative counters because it performs a bulk `$inc` and then floors negative values in a second cancellable call. Cancellation between the calls or concurrent reads can leave or observe negative counter values. | Make the decrement floor-to-zero atomic in the MongoDB write itself, or otherwise guarantee negative values are never persisted or observed. | @Inspect review of 18c8a67ea9633abd0e044a3eafdce29ddefc4d8d..HEAD reported the two-step MongoDB decrement sequence and its cancellation/concurrent-read exposure. |
| RVW-002 | MAJOR | ClientManager.DataAccess/Stores/Implementations/RedisDocumentStore.cs | Redis batch decrement queues `StringDecrementAsync` operations and later writes zero for negative results, so cancellation or concurrent reads can leave or observe negative counter values. | Use an atomic Redis operation, such as a Lua script, that computes `max(0, current - amount)` without storing an intermediate negative value. | @Inspect review of 18c8a67ea9633abd0e044a3eafdce29ddefc4d8d..HEAD reported the pipelined decrement-then-correct flow. |
| RVW-003 | MAJOR | ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs | MongoDB batch increment is not concurrency-correct for expired counters because it preloads documents and replaces expired keys by `_id`, allowing concurrent increments to overwrite each other to `amount`. | Make the reset-or-increment decision atomic per key, for example with an update pipeline or transactional pattern that checks `WindowStart` at write time and returns the actual post-write value. | @Inspect review of 18c8a67ea9633abd0e044a3eafdce29ddefc4d8d..HEAD reported that expired-counter decisions are made from preloaded state before replacement writes. |
| RVW-004 | MINOR | ClientManager.DataAccess/Stores/Interfaces/IDocumentStore.cs | XML documentation no longer accurately describes the counter surface: the summary omits decrement APIs and still says counters are used exclusively by the rate-limit database even though `ResourceAllocationDatabase` now uses them. | Update the XML docs to describe the current counter APIs and consumers accurately. | @Inspect review of 18c8a67ea9633abd0e044a3eafdce29ddefc4d8d..HEAD reported stale interface documentation after the new counter surface was added. |

## Dispositions

| Finding ID | Status | Owner | Evidence | Reply |
|------------|--------|-------|----------|-------|
| RVW-001 | FIXED | @Implement | @Inspect re-review after commit 8d3e217 verified MongoDB decrement floors inside the `FindOneAndUpdate` pipeline, so no negative intermediate counter value is persisted. | Resolved by the follow-up implementation; no approval blocker remains. |
| RVW-002 | FIXED | @Implement | @Inspect re-review after commit 8d3e217 verified Redis decrement uses Lua to compute and store `max(0, current - amount)` atomically while preserving TTL. | Resolved by the follow-up implementation; no approval blocker remains. |
| RVW-003 | FIXED | @Implement | @Inspect re-review after commit 8d3e217 verified MongoDB expired-window increment reset-vs-increment decisions occur inside the update pipeline. | Resolved by the follow-up implementation; no approval blocker remains. |
| RVW-004 | FIXED | @Implement | @Inspect re-review after commit 8d3e217 verified `IDocumentStore` docs cover decrement APIs and `ResourceAllocationDatabase` usage. | Resolved by the documentation follow-up; no approval blocker remains. |

## Approval Gate

- Current verdict: APPROVED
- Approval blockers: None.
- Next reviewer: None; next consumer is @Iterate to record Step 3 approval and continue the loop.

## Review History

| Round | Verdict | Reviewer | Notes |
|-------|---------|----------|-------|
| 1 | CHANGES REQUESTED | @Inspect | Normalized RVW-001 through RVW-004 from review of committed delta 18c8a67ea9633abd0e044a3eafdce29ddefc4d8d..HEAD on feature/hot-path-performance-observability-1-baseline-runtime. |
| 2 | APPROVED | @Inspect | Re-review after commit 8d3e217 marked RVW-001, RVW-002, RVW-003, and RVW-004 fixed. Residual risks remain: Redis and MongoDB had compile/review verification only with no live backend execution, StorageApi still has 31 existing XML-doc warnings, and low-interval runtime lock-wait 503/timeouts plus missing browser screenshots remain for later verification. |
