# Decision Log

## Accepted Decisions

| Decision ID | Scope | Made by | Decision | Rationale |
|-------------|-------|---------|----------|-----------|
| DEC-301 | RuntimeStateClient `// CR: Use fluent API` comments | @Inspect | Defer to later services/observability pass | Pre-existing at baseline; concerns instrumentation builder ergonomics, outside Step 3 structural/naming/doc/retryability scope |

## Waivers And Exceptions

| Decision ID | Applies to | Approved by | Reason | Follow-up |
|-------------|------------|-------------|--------|-----------|
| RVW-N01 | RuntimeStateClient fluent-API comments | @Inspect (non-blocking) | Deferred to a later services/observability pass | Address when instrumentation builder ergonomics are revisited |
| RVW-301 | Step 3 runtime DI/Swagger/UI checks | @Inspect (non-blocking) | Build-only delegated pass | Exercise live DI resolution, `/docs`, and `/services`,`/resource-pools`,`/rate-limits` during orchestrator runtime verification before overall CR closes |
