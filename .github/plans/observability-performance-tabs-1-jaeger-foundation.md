# Plan: Observability & Performance Sidebar Tabs — Step 1: Jaeger Client Foundation

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [observability-performance-tabs-2-observability-page.md](observability-performance-tabs-2-observability-page.md)
> **Parent**: [observability-performance-tabs-overview.md](observability-performance-tabs-overview.md)

## TL;DR

Build the shared layer both new tabs depend on: a configurable Jaeger base URL, a registered `"Jaeger"` HttpClient, a `JaegerApiService` that wraps Jaeger's query API (services, operations, trace search, single trace), strongly-typed response models for Jaeger's JSON, and a reusable `JaegerUnavailableNotice` component plus a connectivity check. No pages yet.

## Iteration Bootstrap

- **Iteration slug**: `observability-performance-tabs`
- **Required evidence**: `dotnet build ClientManager.AdminUI` succeeds; `JaegerApiService` is resolvable from DI; a manual smoke test (temporary call or unit-style probe) shows `GetServicesAsync()` returns data when Jaeger is up and the availability check returns `false` (no exception thrown) when Jaeger is down.
- **UI artifacts to verify**: None directly (no page yet), but the `JaegerUnavailableNotice` component must compile and render in isolation. Verified visually in Steps 2–3.
- **Commit-splitting guidance**: Single commit is acceptable.

## Reference Pattern

There is **no existing Jaeger/trace-querying code** in the repo. Follow these analogous patterns:

In [ClientManager.AdminUI/Services/StatisticsApiService.cs](ClientManager.AdminUI/Services/StatisticsApiService.cs):
- Constructor takes `IHttpClientFactory` and calls `CreateClient("ClientManagerApi")`. Mirror this with `CreateClient("Jaeger")`.
- Methods use `System.Net.Http.Json` helpers and a small `ApiResponseHandler`. Reuse [ClientManager.AdminUI/Services/ApiResponseHandler.cs](ClientManager.AdminUI/Services/ApiResponseHandler.cs) where appropriate, but Jaeger errors must be swallowed into an availability result rather than surfaced.

In [ClientManager.AdminUI/Program.cs](ClientManager.AdminUI/Program.cs):
- `AddHttpClient("ClientManagerApi", …)` with config-driven `BaseAddress` and a dev-only `DangerousAcceptAnyServerCertificateValidator`. Mirror this for `"Jaeger"` using config key `JaegerBaseUrl`.
- Services are registered with `AddScoped<T>()`.

In [ClientManager.AdminUI/appsettings.json](ClientManager.AdminUI/appsettings.json):
- Top-level keys like `"ApiBaseUrl"`. Add `"JaegerBaseUrl"` the same way.

In [ClientManager.AdminUI/Models/TimeRangePreset.cs](ClientManager.AdminUI/Models/TimeRangePreset.cs):
- Small `record` types per file in the `Models` folder. Jaeger response DTOs follow the same one-record-per-concern style.

## Steps

### 1. Add the Jaeger base URL to configuration

Edit [ClientManager.AdminUI/appsettings.json](ClientManager.AdminUI/appsettings.json) and [ClientManager.AdminUI/appsettings.Development.json](ClientManager.AdminUI/appsettings.Development.json) to add a top-level `"JaegerBaseUrl"` key defaulting to `http://localhost:16686` (matching `launch_observability_ui.py`'s default Jaeger port).

### 2. Register the Jaeger HttpClient and service in DI

Edit [ClientManager.AdminUI/Program.cs](ClientManager.AdminUI/Program.cs):
- Add `AddHttpClient("Jaeger", …)` reading `builder.Configuration["JaegerBaseUrl"] ?? "http://localhost:16686"`, copying the existing dev-cert handler block.
- Register `builder.Services.AddScoped<JaegerApiService>();` next to the other `AddScoped` service registrations.

### 3. Create Jaeger response models

Create a new folder `ClientManager.AdminUI/Models/Jaeger/` with one `record` per file matching Jaeger's query API JSON. Minimum shapes (property names must match Jaeger's JSON, which uses camelCase that maps to PascalCase via default System.Text.Json options — verify and add `[JsonPropertyName]` only if needed):

```csharp
// JaegerTrace: traceID + spans + processes
// JaegerSpan: spanID, operationName, startTime (µs epoch), duration (µs), references, tags, processID
// JaegerReference: refType ("CHILD_OF"/"FOLLOWS_FROM"), traceID, spanID
// JaegerKeyValue: key, type, value (tags)
// JaegerProcess: serviceName, tags
// JaegerApiResponse<T>: data (List<T>), errors
```

Keep each record minimal — only the fields the two pages consume (trace ID, span timing, operation name, parent/child references, service name, and the tags needed for the waterfall labels).

### 4. Create `JaegerAvailability` result type

In `ClientManager.AdminUI/Models/Jaeger/`, add a small type (record or readonly struct) representing the outcome of a Jaeger call without throwing — e.g. `JaegerResult<T>(bool IsAvailable, T? Value, string? Message)`. Used so pages can branch to the friendly notice instead of catching exceptions inline.

### 5. Create `JaegerApiService`

Create [ClientManager.AdminUI/Services/JaegerApiService.cs](ClientManager.AdminUI/Services/JaegerApiService.cs). It wraps the `"Jaeger"` HttpClient and exposes (all returning the `JaegerResult<T>` wrapper, never throwing on connection/Jaeger errors):

```csharp
Task<JaegerResult<bool>> CheckAvailabilityAsync(CancellationToken ct);           // GET /api/services
Task<JaegerResult<List<string>>> GetServicesAsync(CancellationToken ct);          // GET /api/services
Task<JaegerResult<List<string>>> GetOperationsAsync(string service, CancellationToken ct); // GET /api/operations?service=
Task<JaegerResult<List<JaegerTrace>>> SearchTracesAsync(JaegerTraceQuery query, CancellationToken ct); // GET /api/traces?...
Task<JaegerResult<JaegerTrace?>> GetTraceByIdAsync(string traceId, CancellationToken ct); // GET /api/traces/{id}
```

- `JaegerTraceQuery` is a small record holding service, operation (optional), start/end (µs epoch or `lookback`), `limit`, and an optional `tagsJson`/free-text filter used by the Observability search. Place it under `Models/Jaeger/`.
- Catch `HttpRequestException`, `TaskCanceledException` (timeout), and JSON failures, returning `IsAvailable = false` with a friendly message. Only genuine successful HTTP 2xx with parseable data yields `IsAvailable = true`.
- Keep methods small (early returns); split any method over 30 lines.

### 6. Create the shared unavailable-notice component

Create [ClientManager.AdminUI/Components/Shared/JaegerUnavailableNotice.razor](ClientManager.AdminUI/Components/Shared/JaegerUnavailableNotice.razor):
- A friendly, non-alarming card (reuse existing card/`cm-*` classes seen in `ActiveAllocations.razor` and the list pages) explaining that trace data comes from a local Jaeger instance and that it appears not to be running or configured.
- Include the exact remediation hint: run `python _scripts/launch_observability_ui.py up` and ensure `JaegerBaseUrl` points at it (default `http://localhost:16686`).
- Accept an optional `[Parameter] string? Message` to surface the specific failure detail from `JaegerResult`.

## Verification

- `dotnet build ClientManager.AdminUI/ClientManager.AdminUI.csproj` compiles without errors or warnings.
- `JaegerApiService` resolves from DI (app starts without DI exceptions).
- With Jaeger running (`python _scripts/launch_observability_ui.py up`) and traffic flowing (`python _scripts/traffic_generator.py --base-url http://localhost:5062 --interval 2.0`), a temporary probe shows `GetServicesAsync()` returns `ClientManager.Api` and `ClientManager.StorageApi`, and `SearchTracesAsync` returns traces with spans.
- With Jaeger **stopped**, every `JaegerApiService` method returns `IsAvailable = false` with a friendly message and **does not throw**.
- **UI: render `JaegerUnavailableNotice` on a temporary scratch route (or the Dashboard, reverted after) — verify it shows the friendly card and remediation command, with no error banner or stack trace.**
- **UI: take a screenshot of the notice component to confirm styling matches existing cards.**
