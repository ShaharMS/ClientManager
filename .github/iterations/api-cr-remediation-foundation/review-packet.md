# Review Packet

## Review Source

- Source type: @Inspect committed-pass review (own inspection)
- Scope: api-cr-remediation-1-foundation-contracts.md
- Baseline: 0a92dfa370fd3e067b9d141a223b70654c195edb
- Range: 0a92dfa..HEAD (c0d07c0) on feature/api-cr-remediation-foundation
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
| RVW-001 | Residual risk | ClientManager.Api/Controllers/StatisticsController.cs | Plan verification step (runtime `/docs` + `/monitor` statistics query binding via `IdentifierList` TypeConverter) was deferred, not executed. This is the single behavior-changing path in the diff. | Exercise `/monitor` statistics views (targetIds + range + granularity) and `/docs` once the full stack is run, before the overall CR closes. Not a blocker for this step: build is green, the `[TypeConverter]`-on-record query-binding pattern is idiomatic/supported by ASP.NET Core `SimpleTypeModelBinder`, and behavior parity with the old `ParseIds` path was confirmed by inspection. | dotnet build Api: 0 warn/0 err; old `ParseIds(string)` NRE'd on absent `targetIds` exactly as new `targetIds.Values` would, so no regression introduced. |

## Dispositions

| Finding ID | Status | Owner | Evidence | Reply |
|------------|--------|-------|----------|-------|
| RVW-001 | Open (non-blocking) | @Implement / orchestrator | n/a | Carry forward to UI verification before CR close. |

## Implementer Rebuttal Response

- **Removed `// CR: Place in configuration, load from there` markers on route fragments** — ACCEPTED. The original `StorageApiRoutes` carried that inline marker, but the parent overview Key Decision explicitly supersedes it: "Immutable route fragments, query-parameter names, and request-shape helpers used across hosts should move into shared code ... This resolves the project-wide reuse concern more cleanly than putting fixed route strings in appsettings." Moving the routes into `ClientManager.Shared.Contracts.Storage.StorageApiRoutes` (immutable, host-agnostic, no `BaseUrl`/timeouts) is the plan-sanctioned resolution. The companion `// CR:` documentation marker is also resolved — the new shared class and every route now carry XML summaries. Removing the markers was correct, not a dropped concern.

## Approval Gate

- Current verdict: APPROVED
- Approval blockers: none
- Next reviewer: n/a (proceed to Step 2)

## Review History

| Round | Verdict | Reviewer | Notes |
|-------|---------|----------|-------|
| 1 | APPROVED | @Inspect | Cumulative delta 0a92dfa..HEAD. All 3 plan steps implemented: shared route/query contracts + `IdentifierList` binder + typed options/validators (StorageApi, ApiVersioning, Observability). Controller-local `ParseIds`/`ParseClientIds` removed; no string-splitting helpers or AdminUI refs remain in Api. Build green (Api 0/0; Shared 1 pre-existing CS8604 in untouched AppLogger.cs). No unsafe type escapes. CR-marker rebuttal accepted. One non-blocking residual risk RVW-001 (deferred runtime binding check). |
