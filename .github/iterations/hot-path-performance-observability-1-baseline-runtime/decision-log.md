# Decision Log

## Accepted Decisions

| Decision ID | Scope | Made by | Decision | Rationale |
|-------------|-------|---------|----------|-----------|
| DEC-001 | Hot Path Performance Observability Step 1 baseline artifact | User via @Iterate | Do not treat 503-heavy rebuilt source baseline as a Step 1 blocker. If the rebuilt artifact is too degraded for comparison, use the provisional baseline as the before comparison anchor, including copying provisional data into the before artifact. | The plan is intended to expose and then resolve current hot-path failures in later steps; many 503s in the before state are part of the problem rather than a reason to stop. |

## Waivers And Exceptions

| Decision ID | Applies to | Approved by | Reason | Follow-up |
|-------------|------------|-------------|--------|-----------|
