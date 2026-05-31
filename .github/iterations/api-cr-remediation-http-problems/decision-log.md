# Decision Log

## Accepted Decisions

| Decision ID | Scope | Made by | Decision | Rationale |
|-------------|-------|---------|----------|-----------|
| DEC-201 | ClientConfigurationsController Update/Delete 404 | @Inspect | Accept leaving Update/Delete without a 404 throw | Those routes declare no `[ProducesResponseType(404)]`; adding 404 is an out-of-scope route-contract change, not part of Step 2's mandatory-lookup audit |
| DEC-202 | Nested optional config documents | @Inspect | Accept keeping nested config returns nullable | Genuinely optional per plan; absence does not imply the parent client is missing |
| DEC-203 | `Func<Exception>` CR markers in internal clients | @Inspect | Defer to Step 3 | Belongs to internal transport structure reorganization |

## Waivers And Exceptions

| Decision ID | Applies to | Approved by | Reason | Follow-up |
|-------------|------------|-------------|--------|-----------|
| RVW-201 | Step 2 runtime RFC 7807 + UI outage checks | @Inspect (non-blocking) | Build-only delegated pass; live 404/409/503 + `/clients`,`/services`,`/monitor` outage/recovery deferred | Exercise during orchestrator runtime verification before overall CR closes |
