# Plan: Code Slim-Down — Step 7: API Exceptions & Instrumentation

> **Status**: 🔲 Not started
> **Prerequisite**: [code-slimdown-6-api-controllers.md](code-slimdown-6-api-controllers.md)
> **Next**: [code-slimdown-8-adminui.md](code-slimdown-8-adminui.md)
> **Parent**: [code-slimdown-overview.md](code-slimdown-overview.md)

## TL;DR

Final backend cleanup: collapse the 20+ near-empty domain exception classes behind a small factory while keeping the base types the global exception filter maps on, apply primary-constructor/modern-C# cleanup across the instrumentation utilities, make `ClientManagerMetrics` declarative, and dedupe the repeated `TagList` construction in `RequestTrackingMiddleware`. With the transport layer gone (step 4), there is no resilience/fail-open dispatch left to refactor here.

## Iteration Bootstrap

- **Iteration slug**: `code-slimdown`
- **Required evidence**: `dotnet build ClientManager.slnx` clean; global exception filter still maps each exception to its declared HTTP status + `[ProducesResponseType]`; metric/trace names unchanged; `git diff --stat` net deletions.
- **UI artifacts to verify**: Error states (404/409/429) still surface correctly in AdminUI; Monitor still shows metrics.
- **Commit-splitting guidance**: (a) exception factory + class consolidation, (b) metrics declarative refactor, (c) middleware TagList dedupe + primary-constructor cleanup.

## Reference Pattern

In [ClientManager.Api/](ClientManager.Api/) exception classes and the global exception filter/middleware:
- Many exceptions are one-line subclasses of `NotFoundException`/`ConflictException`/`RateLimitedException` carrying only a formatted message. The filter switches on the base types to choose the status code.

In [ClientManager.Api/Middlewares/RequestTrackingMiddleware.cs](ClientManager.Api/Middlewares/RequestTrackingMiddleware.cs) and the API metrics definitions:
- Repeated `TagList` construction and repetitive counter/histogram field declarations are consolidation candidates.

## Steps

### 1. Consolidate domain exceptions behind a factory

**Keep** the base types the filter maps on — `NotFoundException`, `ConflictException`, `RateLimitedException` (and any other base the filter switches on). Replace the 20+ specific subclasses that add only a message with a static factory of message builders.

```csharp
public static class DomainErrors
{
    public static NotFoundException Service(string id) => new($"Service '{id}' was not found.");
    public static ConflictException DuplicateService(string id) => new($"Service '{id}' already exists.");
    // ...one line per former subclass
}
```

Replace `throw new SpecificException(...)` with `throw DomainErrors.X(...)`. Confirm the global exception filter still maps every thrown type to the correct status code declared by `[ProducesResponseType]`. Do not change the public error response shape.

### 2. Make `ClientManagerMetrics` declarative

Replace repetitive per-instrument field + creation boilerplate with a compact declarative table/loop or grouped initialization, preserving every meter/instrument **name and unit** verbatim (observability contract).

### 3. Dedupe middleware tagging + modern-C# cleanup

In `RequestTrackingMiddleware`, extract the repeated `TagList` construction into a single helper. Apply primary constructors, expression-bodied members, and target-typed `new` across the instrumentation/middleware utilities where it reduces lines without obscuring intent. Keep nesting ≤2 levels.

## Verification

- `dotnet build ClientManager.slnx` compiles cleanly.
- Exception parity: trigger a not-found (GET unknown id → 404), a conflict (duplicate create → 409), and a rate-limited path (→ 429) and confirm the filter still maps them correctly.
- Metric/trace parity: instrument names + units unchanged (diff against observability notes in `.github/plans/`).
- `git diff --stat` shows net deletions across exceptions + instrumentation.
- **UI: Trigger an error in AdminUI (e.g., create a duplicate Service) and confirm the correct error message/state appears — validates the exception factory + filter mapping.**
- **UI: Open Monitor and confirm metrics still populate. Screenshot Monitor under live traffic.**
