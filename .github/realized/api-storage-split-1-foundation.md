# Plan: Split Public API from Storage Service — Step 1: Foundation

> **Status**: ✅ Completed
> **Prerequisite**: None — this is the first step.
> **Next**: [api-storage-split-2-configuration-split.md](api-storage-split-2-configuration-split.md)
> **Parent**: [api-storage-split-overview.md](api-storage-split-overview.md)

## TL;DR

Create the new internal `ClientManager.StorageApi` host, define the shared contract seam in `ClientManager.Shared`, and add the public API client seam before moving any behavior. This gives the codebase a stable app-to-app boundary so later steps can migrate features incrementally instead of doing a risky big-bang cutover, while keeping one canonical type definition per cross-project contract.

## Reference Pattern

In [../../ClientManager.Api/Program.cs](../../ClientManager.Api/Program.cs):
- Mirror the existing ASP.NET Core bootstrap for versioning, Swagger, JSON enum serialization, logging, and middleware order.
- Keep host setup conventional so the new app looks like the current API host instead of introducing a custom startup model.

In [../../ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs](../../ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs):
- Preserve the pattern of grouping DI registration into focused helper methods.
- Bind options from configuration rather than hard-coding endpoints or provider settings.

In [../../ClientManager.AdminUI/Services/ClientApiService.cs](../../ClientManager.AdminUI/Services/ClientApiService.cs):
- Use typed `HttpClient` wrappers to hide transport details from controllers and higher-level services.
- Keep HTTP error handling at the client layer with `EnsureSuccessStatusCode()` instead of duplicating it at call sites.

In [../../ClientManager.Shared/Models/Requests/CheckAccessRequest.cs](../../ClientManager.Shared/Models/Requests/CheckAccessRequest.cs):
- Follow the existing shared-request pattern for any cross-project command payload.
- Keep shared transport models simple, canonical, and free of host-specific concerns.

In [../../ClientManager.Shared/Models/Responses/AccessResponses.cs](../../ClientManager.Shared/Models/Responses/AccessResponses.cs):
- Reuse existing shared response types instead of creating `ClientManager.Api` and `ClientManager.StorageApi` copies.
- Add new internal response shapes here only if no current shared response already fits.

In [../../ClientManager.Shared/Models/Search/DocumentQuery.cs](../../ClientManager.Shared/Models/Search/DocumentQuery.cs):
- Reuse the existing shared query model for cross-project search payloads.
- Avoid creating separate internal query DTOs when the current shared search model already expresses the request.

## Steps

### 1. Add the new internal host project, then establish the shared contract rule

Create a new ASP.NET Core project at `ClientManager.StorageApi/` and add it to `ClientManager.slnx`. Copy the public API host conventions that should stay aligned across both apps: controller-based endpoints, API versioning, Swagger, XML docs, enum JSON serialization, NLog setup, and middleware registration.

Keep `ClientManager.StorageApi` internal-only in purpose, but still use controllers rather than minimal APIs so it follows the existing documentation and thin-controller guidance.

Before adding any internal endpoint models, review every type that will cross the `ClientManager.Api` to `ClientManager.StorageApi` boundary. Reuse the existing shared entities, requests, responses, search models, and enums first. If a truly new cross-project contract is required, add it once under `ClientManager.Shared` in a clearly named folder such as `Models/Internal/`, rather than creating separate host-local DTOs.

```csharp
public sealed class StorageApiOptions
{
    public required string BaseUrl { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
```

Keep options local to the consuming host unless the exact same options type is referenced by more than one project.

### 2. Move storage registration into the new app, not the public one

Move the storage-facing registration pieces from `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs` into `ClientManager.StorageApi`, then split that registration into storage-specific and application-specific helpers inside the new project.

At this stage, keep `ClientManager.Api` behavior unchanged, but add the new outbound-client registration for talking to `ClientManager.StorageApi`. Introduce only the minimal typed-`HttpClient` setup needed in `ClientManager.Api/Program.cs` or a small DI extension file; the goal is to shift existing registration, not create a second complex registration stack.

### 3. Establish public API client abstractions for later migrations

Add internal client interfaces and wrappers in `ClientManager.Api`, for example under `Services/InternalClients/`, so later steps can switch controllers and services one concern at a time.

Do not model these clients after repositories. Model them around app-level concerns such as configuration catalog operations, runtime access/allocation operations, and statistics/exporter reads. Their method signatures should consume and return shared contract types; if an interface is used only inside `ClientManager.Api`, keep the interface itself local rather than moving it into `ClientManager.Shared` unnecessarily.

```csharp
public interface IConfigurationStoreClient
{
    Task<SearchResult<Service>> SearchServicesAsync(DocumentQuery query, CancellationToken cancellationToken);
    Task<Service?> GetServiceAsync(string id, CancellationToken cancellationToken);
}
```

The wrapper code should stay thin: serialize shared contracts, call the internal endpoint, deserialize shared contracts, and return.

## Verification

- The solution includes the new `ClientManager.StorageApi` project and both app projects compile.
- Any type referenced by both `ClientManager.Api` and `ClientManager.StorageApi` has a single definition in `ClientManager.Shared`; no duplicate internal DTO files exist across the hosts.
- `ClientManager.StorageApi` starts with Swagger and XML comments enabled, even if most endpoints are still placeholders.
- UI: Navigate to `/clients` and verify the list still loads through `ClientManager.Api` without any new error banner.
- UI: Navigate to `/services` and `/resource-pools` and verify both pages still render and fetch data normally.
- UI: Open `/` and verify the dashboard loads without broken layout or startup errors after the new project is introduced.
