# Plan: Harden API/Storage Split Reliability — Step 2: Transport Contracts

> **Status**: ✅ Completed
> **Prerequisite**: [api-storage-split-reliability-1-runtime-parity.md](api-storage-split-reliability-1-runtime-parity.md)
> **Next**: [api-storage-split-reliability-3-read-models-rollout.md](api-storage-split-reliability-3-read-models-rollout.md)
> **Parent**: [api-storage-split-reliability-overview.md](api-storage-split-reliability-overview.md)

## TL;DR

Make the internal HTTP layer explicit and trustworthy. The public API should not leak accidental storage-host semantics such as wrong not-found handling, opaque 5xx transport failures, or inconsistent retry behavior just because the runtime moved behind `HttpClient`.

## Reference Pattern

In [../../ClientManager.AdminUI/Services/ClientApiService.cs](../../ClientManager.AdminUI/Services/ClientApiService.cs):
- Keep transport concerns centralized in the client wrapper rather than spread across controllers.
- Fail deliberately on unexpected responses instead of silently returning misleading defaults.

In [../../ClientManager.Api/Controllers/AccessCheckController.cs](../../ClientManager.Api/Controllers/AccessCheckController.cs):
- Preserve the public route surface and response-code documentation.
- Keep controllers thin; transport-specific error mapping belongs below this layer.

In [../../ClientManager.Api/Middlewares/ErrorHandlingMiddleware.cs](../../ClientManager.Api/Middlewares/ErrorHandlingMiddleware.cs):
- Ensure new exceptions raised by internal client failures map to stable public API responses.
- Preserve consistent retry-after and problem-detail behavior where applicable.

## Steps

### 1. Audit every internal client for response handling bugs

Review all `ClientManager.Api/Services/InternalClients/*` implementations for:

- missing not-found mapping on write paths
- methods that turn “missing nested entry” into “missing client” or vice versa
- `EnsureSuccessStatusCode()` calls that bypass public exception mapping
- resilience behavior that retries or short-circuits the wrong operations

### 2. Align storage-host response shapes with public-host expectations

Where the storage host currently returns ambiguous `200 + null`, broad `404`, or generic `500` responses, adjust the storage controller/service contract so the public typed client can reconstruct the original public API semantics without guesswork.

```csharp
public sealed record StorageApiProblemResponse
{
    public string? ErrorCode { get; init; }
    public int? Status { get; init; }
}
```

Keep cross-host behavior coarse-grained and predictable.

### 3. Validate outage behavior and fast-fail behavior end to end

Review `StorageApiResilienceHandler`, `StorageApiResilienceState`, option binding, and public error middleware together. Confirm that an unavailable storage host becomes a stable public failure mode such as `503`, not a random mix of `HttpRequestException`, timeouts, and controller crashes.

## Verification

- Every internal client method maps expected storage-host failures to deliberate public exceptions instead of raw transport errors.
- Public API routes preserve their documented status codes for not-found, conflict, forbidden, throttled, and unavailable cases after the split.
- Storage-host outage behavior returns a predictable public `503`-style response with retry-after semantics where configured.
- UI: Navigate to `/clients`, `/services`, and `/resource-pools` and verify normal browsing still works through the public API proxy layer.
- UI: Create, edit, and delete one configuration item from the Admin UI and verify the page reflects the expected public result without a generic error toast.
- UI: After a failed internal call scenario, verify the UI shows a stable error state rather than hanging, partially refreshing, or surfacing raw transport text.