# Plan: Multi-Provider Storage Architecture — Step 2: DI Wiring with Keyed Services

> **Status**: ✅ Completed
> **Prerequisite**: [multi-provider-storage-1-config-models.md](multi-provider-storage-1-config-models.md)
> **Next**: [multi-provider-storage-3-database-migration.md](multi-provider-storage-3-database-migration.md)
> **Parent**: [multi-provider-storage-overview.md](multi-provider-storage-overview.md)

## TL;DR

Replace the single `IDocumentStore` registration with multiple keyed registrations — one per `StorageRole`. Each role resolves to its own `IDocumentStore` instance configured by the matching `StorageRoleBinding` (or the default provider). Uses .NET 8 keyed services so downstream databases can inject `[FromKeyedServices(StorageRole.X)] IDocumentStore`.

## Reference Pattern

In [ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs](ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs):
- `RegisterDocumentStore` currently reads a single `PersistenceOptions.Provider` and registers one `IDocumentStore`
- `RegisterRepositories` creates all database instances using `sp.GetRequiredService<IDocumentStore>()`
- This file is the central DI composition root for the storage layer

## Steps

### 1. Add a helper method to resolve a `StorageRoleBinding` for a given role

**File: `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs`**

Create a private static method `ResolveBinding(PersistenceOptions options, StorageRole role)` that:
1. Checks `options.Roles` dictionary for an explicit entry for the role
2. If found, returns that `StorageRoleBinding`
3. If not found, constructs a `StorageRoleBinding` from `options.DefaultProvider` + the matching `Default*` platform options

### 2. Add a helper method to create an `IDocumentStore` from a `StorageRoleBinding`

**File: `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs`**

Create a private static method `CreateDocumentStore(StorageRoleBinding binding)` that:
- For `PersistenceProvider.JsonFile`: creates a `JsonFileDocumentStore` from `binding.JsonFile`
- For `PersistenceProvider.MongoDb`: creates the `MongoClient` (applying TLS/auth settings from `binding.MongoDb`), gets the database, and creates a `MongoDBDocumentStore`
- For `PersistenceProvider.Redis`: creates the `ConnectionMultiplexer` (applying TLS/timeout settings from `binding.Redis`) and creates a `RedisDocumentStore`

**Important**: When multiple roles use the same provider with the same connection string, they should share the underlying client connection (same `MongoClient`, same `ConnectionMultiplexer`). Use a dictionary to deduplicate by connection string.

### 3. Replace `RegisterDocumentStore` with multi-store registration

**File: `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs`**

Replace the current `RegisterDocumentStore(IServiceCollection, PersistenceOptions)` method body:

1. Iterate all `StorageRole` enum values
2. For each role, call `ResolveBinding` to get its binding
3. Register `IDocumentStore` as a **keyed singleton** using `services.AddKeyedSingleton<IDocumentStore>(role, ...)` where the factory calls `CreateDocumentStore`
4. Share connections: cache `IMongoClient` / `IConnectionMultiplexer` by connection string to avoid opening duplicate connections for roles that share the same backend instance

Do **not** register a non-keyed `IDocumentStore` — all consumers must go through keyed resolution from the start.

### 4. Update `MongoDBDocumentStore` constructor to accept `MongoDbStoreOptions`

**File: `ClientManager.DataAccess/Stores/Implementations/MongoDBDocumentStore.cs`**

The constructor currently takes `IMongoDatabase`. Keep this constructor (it's fine), but the *creation* in the DI helper should apply the TLS/auth settings from `MongoDbStoreOptions` when building the `MongoClientSettings`:

- If `UseTls` is true, set `SslSettings.Enabled = true`
- If `TlsCertificatePath` is set, load the PFX and add to `SslSettings.ClientCertificates`
- If `AllowInsecureTls` is true, set `SslSettings.CheckCertificateRevocation = false` and `AllowInsecureTls = true`
- Apply `ConnectTimeout`, `MaxConnectionPoolSize`, `RetryWrites` from options
- If `AuthenticationMechanism` is set, configure credential accordingly

This logic lives in the DI factory method in `ServiceCollectionExtensions`, not in the store class itself.

### 5. Update Redis store creation to apply `RedisStoreOptions`

Same approach — in the DI factory, build `ConfigurationOptions` from `RedisStoreOptions`:

- Set `ConnectTimeout`, `SyncTimeout`
- If `UseTls`, set `Ssl = true`
- If `TlsCertificatePath`, load cert and set `CertificateSelection` callback
- If `AllowInsecureTls`, set `CertificateValidation` to always succeed
- If `Password` is set, apply it
- Set `DefaultDatabase` from `DatabaseIndex`

## Verification

- Project compiles without errors
- All four `StorageRole` keyed `IDocumentStore` registrations exist — no non-keyed registration
- Application starts successfully with the simple default config (`DefaultProvider: JsonFile`)
- **UI: Navigate to the dashboard — verify all pages load without errors (data is still served correctly)**
- **UI: Navigate to the Clients, Services, and Resource Pools list pages — verify data appears normally**
