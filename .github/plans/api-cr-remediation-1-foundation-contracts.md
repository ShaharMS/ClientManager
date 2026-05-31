# Plan: Address ClientManager.Api Review Notes — Step 1: Foundation Contracts and Options

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [api-cr-remediation-2-http-exception-pipeline.md](api-cr-remediation-2-http-exception-pipeline.md)
> **Parent**: [api-cr-remediation-overview.md](api-cr-remediation-overview.md)

## TL;DR

Normalize the pieces other steps will depend on: shared route/query contracts, typed option models, and model-binding helpers. This step removes stringly-typed transport details from controllers and startup code before any service or documentation sweep begins.

## Reference Pattern

In [../../ClientManager.Api/Models/Configuration/StorageApiOptions.cs](../../ClientManager.Api/Models/Configuration/StorageApiOptions.cs):
- Keep configuration as a documented, strongly typed object rather than loose `builder.Configuration[...]` lookups.
- Use property-level XML docs to explain operational meaning, not just the property type.

In [../../ClientManager.Api/Program.cs](../../ClientManager.Api/Program.cs):
- Keep startup readable by pushing binding/registration details into extensions or dedicated configuration helpers.
- Centralize host configuration instead of spreading string keys across the file.

In [../../ClientManager.Shared/Models/Requests/PagedRequest.cs](../../ClientManager.Shared/Models/Requests/PagedRequest.cs):
- Put reusable request-shape semantics in `ClientManager.Shared` so both public and internal layers can rely on the same contract.
- Keep validation or normalization logic close to the shared contract when it is intrinsic to the request shape.

In [../../ClientManager.Api/Controllers/StatisticsController.cs](../../ClientManager.Api/Controllers/StatisticsController.cs):
- Use the current statistics routes as the source inventory for query parameters that should stop being ad-hoc strings and controller-local split helpers.
- Treat the controller-local `ParseIds` helpers as migration targets for a shared binder/converter approach.

## Steps

### 1. Extract immutable cross-host contracts into `ClientManager.Shared`

Create a shared contract surface for the route and query fragments that must stay synchronized across hosts and UI/API callers:

- internal storage route fragments currently encoded in `StorageApiRoutes`
- statistics query-parameter names such as `filterType`, `targetIds`, `clientIds`, `from`, `to`, and `granularity`
- request-shape helpers for comma-separated identifier lists currently parsed inside `StatisticsController`

Prefer small, documented contract types over one giant constants file.

```csharp
public static class StatisticsQueryParameters
{
    public const string FilterType = "filterType";
    public const string TargetIds = "targetIds";
}
```

Keep host-specific values such as `BaseUrl` out of these shared contract types.

### 2. Replace controller-local parsing with a reusable binder or converter

Remove the `ParseIds` and `ParseClientIds` helpers from `StatisticsController` by introducing one reusable mechanism for comma-separated identifiers. The exact implementation can be a custom model binder, `TypeConverter`, or small shared value object, but the goal is the same:

- no string-splitting helpers inside controller files
- one documented parsing rule for repeated identifier lists
- future controllers can opt in without duplicating code

Apply the shared parser to the statistics endpoints first, because they currently contain the inline CR calling this out.

### 3. Introduce documented option types and validators for startup-only configuration

Clean up `Program.cs` and storage-client registration by adding documented option models and explicit validators for the configuration that is currently read inline:

- `StorageApiOptions`
- API versioning configuration currently read from `ApiVersioning`
- observability/exporter configuration currently read from `Observability`

Favor `IValidateOptions<T>` or an equivalent dedicated validator type over long chains of inline `.Validate(...)` calls.

```csharp
public sealed class StorageApiOptionsValidator : IValidateOptions<StorageApiOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageApiOptions options);
}
```

This step should make later DI-extension cleanup mechanical instead of speculative.

## Verification

- `dotnet build ClientManager.Shared/ClientManager.Shared.csproj`
- `dotnet build ClientManager.Api/ClientManager.Api.csproj`
- Run the API and confirm `http://localhost:5062/docs` still exposes the statistics endpoints with the expected query parameter names after the binder/contract extraction.
- UI: Navigate to `/monitor` and exercise at least one statistics view that uses `targetIds`, date range, and granularity filters; verify the page still loads data rather than failing query binding.
- UI: Navigate to `/services` and `/resource-pools`; verify list pages still load through the public API after shared contract extraction.
- UI: Navigate to `/clients/{id}` for an existing client and verify nested configuration data still loads without a generic error banner.

## Iteration Bootstrap Metadata

- **Recommended iteration slug**: `api-cr-remediation-foundation`
- **Evidence to preserve**: diff of new shared contract/binder/option files; build logs for `ClientManager.Shared` and `ClientManager.Api`; one note or screenshot proving the statistics page still binds filter parameters correctly.
- **UI pages to check**: `/monitor`, `/services`, `/resource-pools`, `/clients/{id}`
- **Commit guidance**: if both `ClientManager.Shared` and `ClientManager.Api` change, keep this step in one commit focused on contract extraction and option binding only.