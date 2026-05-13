# Review Packet

## Review Source

- Source type: @Inspect review of committed delta
- Scope: .github/plans/hot-path-performance-observability-2-tracing-logs.md; 4fc55826f413194b36697123a56a0d3326cc71c5..HEAD on feature/hot-path-performance-observability-1-baseline-runtime
- Baseline: 4fc55826f413194b36697123a56a0d3326cc71c5
- Reviewer: @Inspect

## Review Checklist

- [x] Plan intent reviewed
- [ ] Verification claims checked
- [ ] Repository conventions checked
- [ ] Shared package boundaries checked
- [ ] Naming and structure checked
- [ ] Nesting and complexity checked
- [x] Risks and regressions checked

## Findings

| Finding ID | Severity | File | Concern | Required action | Evidence |
|------------|----------|------|---------|-----------------|----------|
| RVW-001 | MAJOR | ClientManager.Api/Services/InternalClients/Implementations/RuntimeStateClient.cs<br>ClientManager.StorageApi/Services/Implementations/ResourceAllocationService.cs | New hot-path histograms include per-allocation IDs as metric tags, violating Step 2 bounded/low-cardinality tag requirement and risking one time series per allocation/release. | Remove allocation_id/allocationId from histogram metric tag sets, especially StorageClientCallDuration, ResourceReleaseDuration, and StorageApi release duration recording. Keep allocation IDs on spans or structured logs if needed. | RuntimeStateClient release metrics share a tag list that adds allocation_id; ResourceAllocationService records release duration after adding allocationId. |

## Dispositions

| Finding ID | Status | Owner | Evidence | Reply |
|------------|--------|-------|----------|-------|
| RVW-001 | FIXED | @Implement | Removed allocation ID tags from the Api storage-client histogram tag builder and the StorageApi resource duration histogram tag builder. Focused searches found no removed histogram allocation-tag patterns. | Allocation IDs remain on spans, structured logs, request values, and existing non-histogram counter behavior outside the finding scope. |

## Approval Gate

- Current verdict: CHANGES REQUESTED pending re-review
- Approval blockers: RVW-001 addressed by @Implement; pending @Inspect confirmation
- Next reviewer: @Inspect

## Review History

| Round | Verdict | Reviewer | Notes |
|-------|---------|----------|-------|
| 1 | CHANGES REQUESTED | @Inspect | Normalized latest committed-delta review; opened RVW-001 for high-cardinality allocation ID tags on hot-path histograms. |
