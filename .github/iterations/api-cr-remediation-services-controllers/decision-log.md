# Decision Log

## Accepted Decisions

| Decision ID | Scope | Made by | Decision | Rationale |
|-------------|-------|---------|----------|-----------|
| DEC-401 | Catalog Update return shape | @Inspect | Accept preserving prior behavior (`entity with { Id = id }`, original entity sent) | Behavior-faithful migration; no regression introduced |

## Waivers And Exceptions

| Decision ID | Applies to | Approved by | Reason | Follow-up |
|-------------|------------|-------------|--------|-----------|
| RVW-401 | Step 4 runtime UI dashboard checks | @Inspect (non-blocking) | Delegated build-only pass | Confirm `/monitor`, `/`, `/clients`, `/services`, `/resource-pools`, `/rate-limits` render through new services during orchestrator runtime verification |
| RVW-402 | StatisticsController.GetOverview missing `<param name="cancellationToken">` | @Inspect (non-blocking) | Pre-existing doc gap, predates Step 4; hard `<summary>` rule satisfied | Can be tidied in Step 5 documentation pass |
