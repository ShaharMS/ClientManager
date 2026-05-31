# Review Packet

## Review Source

- Source type: Direct inspection (@Inspect)
- Scope: api-cr-remediation-2-http-exception-pipeline.md
- Baseline: f458b78
- Range reviewed: f458b78..HEAD (537f0f1) on feature/api-cr-remediation-http-problems
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
| (none) | n/a | n/a | No material findings. | n/a | See gate notes below. |

## Rebuttal Responses (implementer non-changes)

| Topic | Implementer claim | @Inspect response |
|-------|-------------------|-------------------|
| ClientConfigurationsController Update/Delete left without 404 | Those routes declare no `[ProducesResponseType(404)]`; adding 404 is a route-contract change outside this step's not-found audit. | ACCEPTED. Step 2's audit targets the `GetByIdAsync(...) ?? throw` nullable-to-exception pattern for top-level GET-by-id routes. Update/Delete do not use that pattern and their existing contract declares no 404. Expanding their contract is out of scope; storage 404 still surfaces as an unexpected 500, which is unchanged pre-existing behavior, not a regression introduced here. |
| Nested optional config documents kept nullable | Genuinely optional per plan. | ACCEPTED. The plan explicitly preserves nullable returns "where the route semantics are genuinely optional, such as nested optional configuration documents that do not automatically imply the parent resource is missing." `GetOptionalForClientAsync` is correctly retained for those; only the four top-level resource lookups were promoted to non-nullable + boundary throw. |
| `Func<Exception>` CR comments in internal clients left for Step 3 | Deferred to Step 3. | ACCEPTED. Step 3 (`internal-transport-structure`) owns internal-transport reorganization; deferring those markers there keeps the commit boundaries the overview prescribes. No leftover dead code results from the deferral. |

## Approval Gate

- Current verdict: APPROVED
- Approval blockers: none
- Next reviewer: n/a (proceed to Step 3)

### Gate Results

- Scope evidence gate: PASS - read plan, overview, handoff, packet, decision-log; reviewed focused `git diff f458b78..HEAD` for all 23 changed code files; build run.
- Plan intent gate: PASS - base `HttpProblemException` carries status/title/detail/optional retry-after; all expected exceptions derive from it; mandatory 404s for the four top-level resources (client, service, resource pool, global rate limit) moved to the internal-client boundary; controllers no longer inspect nullability; middleware reduced to one problem path + unexpected path.
- Verification gate: PASS (with residual risk) - build run with concrete evidence (0 errors); edited-file diagnostics clean. Each exception's (status, title, retry-after) was confirmed identical to the old per-type middleware mapping, so the refactor is behavior-preserving by static inspection. Live 404/409/503 + UI outage flows deferred to orchestrator per delegated scope; residual risk low because response status/title/detail are sourced from the same typed exceptions the prior middleware already produced.
- Type safety gate: PASS - no `any`-equivalent unsafe escapes; nullable annotations tightened (`Task<T?>` -> `Task<T>`) with null guards at the boundary; build clean. The lone CS8604 warning is pre-existing in untouched `ClientManager.Shared/Logging/AppLogger.cs`.
- Convention gate: PASS - controllers stay thin and XML-documented; `[ProducesResponseType(404)]` still matches actual produced 404s (now thrown at boundary); API does not reference AdminUI; exceptions remain under `Models/Exceptions`.
- Complexity gate: PASS - middleware collapsed from ~10 catch blocks to two; one small private helper extracted; nesting reasonable.
- Regression gate: PASS - status/title/retry-after mappings verified equivalent to prior behavior for all nine exception types; ClientConfig GetById path now throws `ClientNotFoundException` directly instead of via controller `?? throw`, producing the same 404; removed `GetOptionalAsync` helper confirmed unused.

## Review History

| Round | Verdict | Reviewer | Notes |
|-------|---------|----------|-------|
| 1 | APPROVED | @Inspect | f458b78..537f0f1. Behavior-preserving exception-pipeline refactor; build clean (0 errors, 1 pre-existing unrelated warning). Three implementer non-changes accepted with direct rationale. Residual risk: live 404/409/503 + UI flows deferred to orchestrator. |
