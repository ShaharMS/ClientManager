# Review Packet

## Review Source

- Source type: @Inspect re-review after commit c360238
- Scope: .github/plans/hot-path-performance-observability-2-tracing-logs.md; RVW-001 resolution in committed Step 2 delta on feature/hot-path-performance-observability-1-baseline-runtime
- Baseline: 4fc55826f413194b36697123a56a0d3326cc71c5; re-review commit c360238
- Reviewer: @Inspect

## Review Checklist

- [x] Plan intent reviewed
- [x] Verification claims checked
- [x] Repository conventions checked
- [x] Shared package boundaries checked
- [x] Naming and structure checked
- [x] Nesting and complexity checked
- [x] Risks and regressions checked

## Findings

| Finding ID | Severity | File | Concern | Required action | Evidence |
|------------|----------|------|---------|-----------------|----------|
| RVW-001 | MAJOR | ClientManager.Api/Services/InternalClients/Implementations/RuntimeStateClient.cs<br>ClientManager.StorageApi/Services/Implementations/ResourceAllocationService.cs | New hot-path histograms include per-allocation IDs as metric tags, violating Step 2 bounded/low-cardinality tag requirement and risking one time series per allocation/release. | Remove allocation_id/allocationId from histogram metric tag sets, especially StorageClientCallDuration, ResourceReleaseDuration, and StorageApi release duration recording. Keep allocation IDs on spans or structured logs if needed. | RuntimeStateClient release metrics share a tag list that adds allocation_id; ResourceAllocationService records release duration after adding allocationId. |

## Dispositions

| Finding ID | Status | Owner | Evidence | Reply |
|------------|--------|-------|----------|-------|
| RVW-001 | FIXED | @Implement | @Inspect confirmed RuntimeStateClient.cs and ResourceAllocationService.cs histogram tag builders no longer include allocation_id/allocationId. Api and StorageApi targeted builds pass; VS Code diagnostics are clean; git diff --check is clean. | Allocation IDs remain only on spans, structured logs, request values, or the existing non-histogram ResourceReleased counter. |

## Approval Gate

- Current verdict: APPROVED
- Approval blockers: None
- Next reviewer: None; return to @Iterate
- Residual risks/test gaps: Full solution build could not be re-run cleanly during re-review because an already-running ClientManager.AdminUI process locked ClientManager.Shared.dll; an earlier full solution build passed during implementation. OTLP export against a real collector remains unverified because no local collector endpoint was available.

## Review History

| Round | Verdict | Reviewer | Notes |
|-------|---------|----------|-------|
| 1 | CHANGES REQUESTED | @Inspect | Normalized latest committed-delta review; opened RVW-001 for high-cardinality allocation ID tags on hot-path histograms. |
| 2 | APPROVED | @Inspect | Re-review after commit c360238 confirmed RVW-001 fixed. RuntimeStateClient.cs and ResourceAllocationService.cs histogram tag builders no longer include allocation_id/allocationId; remaining allocation IDs are outside hot-path histogram tags. Targeted Api and StorageApi builds, VS Code diagnostics, and git diff --check were clean; full solution rebuild and real OTLP collector export remain noted test gaps. |
