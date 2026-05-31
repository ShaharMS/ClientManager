# Plan: Address ClientManager.Api Review Notes — Step 2: HTTP Exception Pipeline

> **Status**: 🔲 Not started
> **Prerequisite**: [api-cr-remediation-1-foundation-contracts.md](api-cr-remediation-1-foundation-contracts.md)
> **Next**: [api-cr-remediation-3-internal-transport-structure.md](api-cr-remediation-3-internal-transport-structure.md)
> **Parent**: [api-cr-remediation-overview.md](api-cr-remediation-overview.md)

## TL;DR

Make expected API failures explicit and transport-safe. Controllers should stop throwing based on nullable returns, and the middleware should stop being the only place that knows HTTP status/title/detail semantics.

## Reference Pattern

In [../../ClientManager.Api/Middlewares/ErrorHandlingMiddleware.cs](../../ClientManager.Api/Middlewares/ErrorHandlingMiddleware.cs):
- Keep one centralized RFC 7807 writer for the public host.
- Preserve retry-after behavior for throttling and unavailable responses.

In [../../ClientManager.Api/Services/Interfaces/IAccessControlService.cs](../../ClientManager.Api/Services/Interfaces/IAccessControlService.cs):
- Treat typed exceptions as part of the service contract, not as an implementation accident.
- Keep the controller surface simple by documenting which failures are expected below it.

In [../../ClientManager.Api/Controllers/AccessCheckController.cs](../../ClientManager.Api/Controllers/AccessCheckController.cs):
- Use response-code documentation as the public contract that the exception pipeline must satisfy.
- Keep controller logic to request forwarding and response shaping only.

## Steps

### 1. Introduce a base HTTP/problem exception contract

Add a shared base exception for expected public-host failures. The exact name can follow repo conventions, but it should carry at least:

- HTTP status code
- problem title
- public detail/message
- optional retry-after or similar response metadata when the failure type needs it

```csharp
public abstract class HttpProblemException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
}
```

Refactor the existing API exceptions so the middleware no longer needs a large type-by-type status mapping table to understand normal failures.

### 2. Push mandatory not-found/conflict decisions below controllers

Audit controller/service/internal-client flows where a nullable return is immediately converted into an exception in the controller, for example:

- `GetByIdAsync(...) ?? throw ...`
- controller-side not-found wrapping around internal storage clients
- internal clients that return `null` for a top-level resource even though the public route contract is “404 or entity”

For mandatory lookups, move the typed throw into the public service or the internal client boundary so the controller never needs to inspect nullability to determine HTTP behavior.

Keep nullable returns only where the route semantics are genuinely optional, such as nested optional configuration documents that do not automatically imply the parent resource is missing.

### 3. Simplify `ErrorHandlingMiddleware` around the new contract

Once typed exceptions carry status/title/detail, reduce the middleware to:

- handling one base HTTP/problem exception path
- preserving retry-after headers for throttled or unavailable responses
- logging expected 4xx failures at the lower severity chosen for this project
- treating only unexpected failures as 5xx + error-level logs

This is also the point to apply the review note about log severity policy instead of keeping it implicit across many `catch` blocks.

## Verification

- `dotnet build ClientManager.Api/ClientManager.Api.csproj`
- Use Swagger or an HTTP client to verify at least one 404 path, one 409 path, and one 503-style path still return RFC 7807 responses with stable title/detail values.
- Stop the storage host temporarily and verify the public API returns a deliberate unavailable response rather than a raw `HttpRequestException` or controller crash.
- UI: Navigate to `/clients` and `/services` while the backend is healthy; verify no regressions in normal list/detail flows.
- UI: Navigate to `/monitor` or another statistics-heavy page during a forced storage outage scenario; verify the UI shows a stable error state instead of hanging or surfacing raw transport text.
- UI: Re-enable the storage host and verify the affected pages recover without requiring a hard browser refresh.

## Iteration Bootstrap Metadata

- **Recommended iteration slug**: `api-cr-remediation-http-problems`
- **Evidence to preserve**: one successful 404/409/503 response sample; one note about middleware log severity behavior; one browser check showing graceful UI handling during an unavailable backend.
- **UI pages to check**: `/clients`, `/services`, `/monitor`
- **Commit guidance**: keep exception-type changes and middleware rewrites together; do not mix folder/namespace churn from the next step into the same commit.