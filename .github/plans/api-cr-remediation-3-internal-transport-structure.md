# Plan: Address ClientManager.Api Review Notes — Step 3: Internal Transport Structure

> **Status**: 🔲 Not started
> **Prerequisite**: [api-cr-remediation-2-http-exception-pipeline.md](api-cr-remediation-2-http-exception-pipeline.md)
> **Next**: [api-cr-remediation-4-services-and-controllers.md](api-cr-remediation-4-services-and-controllers.md)
> **Parent**: [api-cr-remediation-overview.md](api-cr-remediation-overview.md)

## TL;DR

Reorganize the storage-facing API layer so folder structure, namespaces, and names reflect responsibility. Transport plumbing should stop leaking through `Services/InternalClients`, ambiguous helper APIs should be renamed or simplified, and the remaining internal interfaces/implementations should be documented consistently.

## Reference Pattern

In [../../ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs](../../ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs):
- Keep DI registration behind one clearly named extension surface rather than scattered extension files with overlapping responsibilities.
- Make method names describe what they register instead of using application-wide umbrella names.

In [../../ClientManager.Api/Services/InternalClients/StorageApiResilienceHandler.cs](../../ClientManager.Api/Services/InternalClients/StorageApiResilienceHandler.cs):
- Use this file as the inventory of transport-only responsibilities that should be grouped together after the move.
- Preserve the narrow-scope resilience behavior, but make retryability decisions explicit rather than suffix-based guesswork.

In [../../ClientManager.Api/Services/Implementations/AccessControlService.cs](../../ClientManager.Api/Services/Implementations/AccessControlService.cs):
- Keep public API services slim wrappers over internal abstractions where appropriate.
- Use XML docs and `<inheritdoc />` intentionally instead of leaving transport-layer interfaces undocumented.

## Steps

### 1. Rename and flatten the internal folder structure

Apply the structural review notes to the API project:

- rename `Services/InternalClients` to `Services/Internal`
- remove unnecessary extra nesting such as `Interfaces/Configuration` and `Implementations/Configuration` where the folder level adds no real ownership boundary
- move storage-transport-specific helper classes into a dedicated `Utils/StorageApi` area

The transport-only bucket should include classes such as:

- `StorageApiResilienceHandler`
- `StorageApiResilienceState`
- `StorageApiResponseReader`
- storage-client registration helpers
- route-contract adapters that remain API-local after Step 1

### 2. Fix namespaces and DI registration names to match the new ownership model

After the folder move, update namespaces and registration entry points so they describe the actual responsibilities. This includes:

- renaming `AddClientManager(...)` to a service-registration name that matches what the extension really adds
- merging or otherwise consolidating `StorageApiClientServiceCollectionExtensions` into the main DI registration surface once the options/validators from Step 1 exist
- ensuring every namespace matches its folder path

### 3. Document and simplify the internal transport helpers

Sweep the moved internal transport code for the inline review notes:

- add XML documentation to currently undocumented interfaces and helper classes
- use `<inheritdoc />` where the interface docs are the source of truth
- rename vague parameters such as `emptyMessage` to names that describe the error condition being reported
- simplify helper signatures that currently accept exception-factory delegates when a direct exception instance, dedicated mapper, or clearer abstraction would do

```csharp
public static Task<T> ReadRequiredAsync<T>(
    HttpResponseMessage response,
    CancellationToken cancellationToken,
    string missingPayloadErrorMessage)
```

### 4. Replace retry heuristics with explicit retryability metadata

Remove the `POST + path ends with /search` heuristic from `StorageApiResilienceHandler`. Make retryability explicit per route or per client registration so the handler is operating on a declared contract rather than string suffix guesses.

The goal is to make it obvious why a given operation retries, not to infer that from a URI shape.

## Verification

- `dotnet build ClientManager.Api/ClientManager.Api.csproj`
- Start the API and confirm dependency injection still resolves all controllers after the namespace and registration move.
- `http://localhost:5062/docs` still loads and shows the same public API surface after the internal folder move.
- UI: Navigate to `/services`, `/resource-pools`, and `/rate-limits`; verify list pages load without startup or DI failures.
- UI: Open one editor page each at `/services/{id}` and `/resource-pools/{id}`; verify the pages can load existing data through the refactored transport layer.
- UI: Trigger one runtime operation through the UI that hits statistics or CRUD refresh logic and verify no raw transport exception text is surfaced.

## Iteration Bootstrap Metadata

- **Recommended iteration slug**: `api-cr-remediation-internal-structure`
- **Evidence to preserve**: post-move build output; note of the final folder/namespace layout; proof that retryability is declared explicitly rather than inferred from `/search` suffixes.
- **UI pages to check**: `/services`, `/services/{id}`, `/resource-pools`, `/resource-pools/{id}`, `/rate-limits`
- **Commit guidance**: if the namespace churn is large, isolate this step in its own commit and avoid mixing it with controller/service migrations.