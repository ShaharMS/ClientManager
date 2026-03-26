# Plan: Multi-Provider Storage Architecture — Step 3: Database Layer Migration

> **Status**: 🔲 Not started
> **Prerequisite**: [multi-provider-storage-2-di-wiring.md](multi-provider-storage-2-di-wiring.md)
> **Next**: [multi-provider-storage-4-validation-config.md](multi-provider-storage-4-validation-config.md)
> **Parent**: [multi-provider-storage-overview.md](multi-provider-storage-overview.md)

## TL;DR

Update every `*Database` implementation and entity repository registration to resolve its `IDocumentStore` via a keyed service lookup using the correct `StorageRole`. After this step, each database talks to its own role-specific store.

## Reference Pattern

In [ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs](ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs) — `RegisterRepositories`:
- Currently all database classes are registered with `sp.GetRequiredService<IDocumentStore>()` (the single non-keyed instance)
- After this step, each uses `sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.X)`

In [ClientManager.DataAccess/Databases/Implementations/ClientConfigurationDatabase.cs](ClientManager.DataAccess/Databases/Implementations/ClientConfigurationDatabase.cs):
- Constructor takes `IDocumentStore store` — this pattern is preserved, the change is only in how DI provides the argument

## Steps

### 1. Update `RegisterRepositories` to use keyed service resolution

**File: `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs`**

Change each registration in `RegisterRepositories`:

**Configuration role** (clients, services, pools, global rate limits):
```csharp
services.AddSingleton<IClientConfigurationDatabase>(sp =>
    new ClientConfigurationDatabase(
        sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration)));

services.AddSingleton<IEntityRepository<Service>>(sp =>
    new EntityRepository<Service>(
        sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration),
        "services", s => s.Id));

services.AddSingleton<IEntityRepository<ResourcePool>>(sp =>
    new EntityRepository<ResourcePool>(
        sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration),
        "resource_pools", r => r.Id));

services.AddSingleton<IGlobalRateLimitDatabase>(sp =>
    new GlobalRateLimitDatabase(
        sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration)));
```

**RateLimiting role**:
```csharp
services.AddSingleton<IRateLimitStateDatabase>(sp =>
    new RateLimitStateDatabase(
        sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.RateLimiting)));
```

**Allocations role**:
```csharp
services.AddSingleton<IResourceAllocationDatabase>(sp =>
    new ResourceAllocationDatabase(
        sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Allocations)));
```

**Statistics role**:
```csharp
services.AddSingleton<IUsageSnapshotDatabase>(sp =>
    new UsageSnapshotDatabase(
        sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Statistics),
        sp.GetRequiredService<IClientConfigurationDatabase>()));
```

### 2. Verify no direct `IDocumentStore` injections remain

Search the entire codebase for any constructor or `GetRequiredService<IDocumentStore>()` calls that do NOT use a keyed lookup. The only places `IDocumentStore` should appear as a constructor parameter are the database implementations — and those are all constructed explicitly in `RegisterRepositories` with the correct keyed instance.

No class should inject `IDocumentStore` via DI auto-resolution (i.e., no `[FromServices] IDocumentStore`). They should always receive it through explicit construction in the DI registration.

### 4. Add the `StorageRole` using directive where needed

**File: `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs`**

Add `using ClientManager.Shared.Models.Enums;` if not already present (it should be, since `PersistenceProvider` is already used).

## Verification

- Project compiles without errors
- Application starts with default config — all four `StorageRole` stores are resolved
- No runtime DI resolution errors (test by hitting a few endpoints)
- `GET /api/v1/clients` returns data (Configuration store works)
- `POST /api/v1/access-check` processes a request (RateLimiting store works)
- Resource allocation acquire/release works (Allocations store works)
- Statistics endpoints return data (Statistics store works)
- **UI: Navigate to the dashboard overview page — verify system stats load correctly**
- **UI: Navigate to the Clients list, click a client to open details — verify service and resource pool settings display**
- **UI: Navigate to the Resource Pools page — verify active allocation counts are shown**
- **UI: Check the time-series charts — verify usage data still renders without errors**
