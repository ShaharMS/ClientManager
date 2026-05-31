# Decision Log

## Accepted Decisions

| Decision ID | Scope | Made by | Decision | Rationale |
|-------------|-------|---------|----------|-----------|
| DEC-001 | Step 1 / route CR markers | @Inspect | Accept removal of `// CR: Place in configuration` markers on storage route fragments | Overview Key Decision places immutable route fragments in `ClientManager.Shared`, not appsettings; marker superseded, not dropped |

## Waivers And Exceptions

| Decision ID | Applies to | Approved by | Reason | Follow-up |
|-------------|------------|-------------|--------|-----------|
| RVW-001 | Step 1 runtime statistics binding check | @Inspect (non-blocking) | Delegated pass was build-only; runtime `/docs` + `/monitor` IdentifierList binding check deferred | Exercise statistics query binding via `/monitor` and `/docs` before the overall CR closes |
