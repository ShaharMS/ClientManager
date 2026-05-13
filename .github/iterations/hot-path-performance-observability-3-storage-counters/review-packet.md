# Review Packet

## Review Source

- Source type: @Inspect review of committed delta
- Scope: .github/plans/hot-path-performance-observability-3-storage-counters.md; comparison 18c8a67ea9633abd0e044a3eafdce29ddefc4d8d..HEAD on feature/hot-path-performance-observability-1-baseline-runtime
- Baseline: 18c8a67ea9633abd0e044a3eafdce29ddefc4d8d
- Reviewer: @Inspect

## Review Checklist

- [x] Plan intent reviewed
- [ ] Verification claims checked
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
| RVW-001 | OPEN | @Implement | Normalized from latest @Inspect review; no implementer response has been recorded for this round. | Awaiting implementation change or supported disposition. |
| RVW-002 | OPEN | @Implement | Normalized from latest @Inspect review; no implementer response has been recorded for this round. | Awaiting implementation change or supported disposition. |
| RVW-003 | OPEN | @Implement | Normalized from latest @Inspect review; no implementer response has been recorded for this round. | Awaiting implementation change or supported disposition. |
| RVW-004 | OPEN | @Implement | Normalized from latest @Inspect review; no implementer response has been recorded for this round. | Awaiting documentation update or supported disposition. |

## Approval Gate

- Current verdict: CHANGES REQUESTED
- Approval blockers: RVW-001, RVW-002, RVW-003. RVW-004 remains open as a minor documentation finding.
- Next reviewer: @Inspect after @Implement addresses the open findings.

## Review History

| Round | Verdict | Reviewer | Notes |
|-------|---------|----------|-------|
| 1 | CHANGES REQUESTED | @Inspect | Normalized RVW-001 through RVW-004 from review of committed delta 18c8a67ea9633abd0e044a3eafdce29ddefc4d8d..HEAD on feature/hot-path-performance-observability-1-baseline-runtime. |
