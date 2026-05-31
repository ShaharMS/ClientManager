# Commit Packet

## Commit Intent

- Pass type: Initial implementation pass for Step 4 (delegated)
- Plan step: api-cr-remediation-4-services-and-controllers.md
- Scope: Single commit covering all `ClientManager.Api` Step 4 source changes (18 new public service files = 9 interfaces + 9 implementations, DI registration, 9 migrated controllers) plus the Step 4 iteration-state/bookkeeping files.
- Reason this is one commit: Delegated initial implementation pass. The service extraction, DI registration, and controller migration are one cohesive change — controllers cannot compile without the new interfaces/registrations, so they ship together.

## Candidate Files

| Path | Include | Reason |
|------|---------|--------|
| ClientManager.Api/Services/Interfaces/*.cs (9 files) | Yes | New public service interfaces |
| ClientManager.Api/Services/Implementations/*.cs (9 files) | Yes | New public service implementations |
| ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs | Yes | DI registration of 9 new services |
| ClientManager.Api/Controllers/*.cs (9 files) | Yes | Migrated to inject public interfaces |
| .github/iterations/api-cr-remediation-services-controllers/** | Yes | Iteration state/bookkeeping for this pass |
| .github/agent-progress/api-cr-remediation-services-controllers.md | Yes | Progress note for this pass |

## Gitflow Decision

- Starting branch: feature/api-cr-remediation-internal-structure (tip c4d682f)
- Target branch: feature/api-cr-remediation-services-controllers
- Branch action: Created `feature/api-cr-remediation-services-controllers` from the c4d682f tip (chained on approved Steps 1-3) and switched to it before staging. Did NOT branch from main.

## Commit Message

```text
feat(api): extract public services and migrate controllers (Step 4)

Introduce public API service interfaces + implementations for the
direct-internal-client controller domains (client configuration plus
nested service/resource-pool/global-rate-limit settings, service catalog,
resource-pool catalog, global-rate-limit catalog, statistics, metrics).
Register them in AddPublicApiServices and migrate every affected
controller to inject the public interface so controllers only bind and
normalize inputs and delegate. Internal storage transport clients are no
longer injected into any controller. Centralize IdentifierList
normalization and client-summary paging in StatisticsService and align
404 ProducesResponseType annotations.

Plan: .github/plans/api-cr-remediation-4-services-and-controllers.md
Pass: initial implementation (Step 4)
```

## Result

- Commit hash: (to be folded into closeout — recorded post-commit in summary)
- Push result: (set-upstream push to origin)
- Workspace status after commit: commit-packet.md + timeline.md left dirty by design (cannot embed their own commit hash)
- Remaining uncommitted files: commit-packet.md Commit History row + timeline.md result entry — deferred to closeout commit
- Follow-up needed: Orchestrator closeout commit folds in this packet's Commit History row and the timeline result entry.

## Commit History

| Pass | Commit | Branch | Notes |
|------|--------|--------|-------|
| 1 (initial impl, Step 4) | (recorded at closeout) | feature/api-cr-remediation-services-controllers | 18 new service files + DI + 9 controllers migrated; build green |
