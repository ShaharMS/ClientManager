# Plan: Multi-Provider Storage Architecture — Step 1: Configuration Models & Storage Roles

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [multi-provider-storage-2-di-wiring.md](multi-provider-storage-2-di-wiring.md)
> **Parent**: [multi-provider-storage-overview.md](multi-provider-storage-overview.md)

## TL;DR

Create the shared `StorageRole` enum, per-platform options classes (`MongoDbStoreOptions`, `RedisStoreOptions`, `JsonFileStoreOptions`), a `StorageRoleBinding` that maps a role to a provider + its settings, and a redesigned `PersistenceOptions` that holds everything together. These types are the foundation for all subsequent sub-plans.

## Reference Pattern

In [ClientManager.Api/Models/Configuration/PersistenceOptions.cs](ClientManager.Api/Models/Configuration/PersistenceOptions.cs):
- Currently a flat class with `Provider`, `JsonFileDataDirectory`, `MongoDbConnectionString`, `MongoDbDatabaseName`, `RedisConnectionString`
- Binds from the `"Persistence"` section of `appsettings.json`
- This class will be **replaced** by the new structure

In [ClientManager.Shared/Models/Enums/PersistenceProvider.cs](ClientManager.Shared/Models/Enums/PersistenceProvider.cs):
- Existing enum with `JsonFile`, `MongoDb`, `Redis` — will be extended with `Lucene` in a future plan

## Steps

### 1. Create `StorageRole` enum

**File: `ClientManager.Shared/Models/Enums/StorageRole.cs`**

```csharp
public enum StorageRole
{
    Configuration,
    RateLimiting,
    Allocations,
    Statistics
}
```

Add XML docs explaining what each role covers:
- `Configuration` — Client configs, services, resource pools, global rate limits
- `RateLimiting` — Rate limit state counters (fixed window, sliding window, token bucket)
- `Allocations` — Resource allocation documents and their maintained atomic counters
- `Statistics` — Usage snapshot time-series data

### 2. Create `MongoDbStoreOptions` class

**File: `ClientManager.Api/Models/Configuration/MongoDbStoreOptions.cs`**

Properties to include:
- `ConnectionString` (string, required)
- `DatabaseName` (string, default `"ClientManager"`)
- `UseTls` (bool, default `false`)
- `TlsCertificatePath` (string?, optional — path to PFX client certificate)
- `TlsCertificatePassword` (string?, optional)
- `AllowInsecureTls` (bool, default `false` — skips cert validation, dev only)
- `AuthenticationMechanism` (string?, optional — e.g. `"SCRAM-SHA-256"`, `"MONGODB-X509"`)
- `ConnectTimeoutSeconds` (int, default `30`)
- `MaxConnectionPoolSize` (int, default `100`)
- `RetryWrites` (bool, default `true`)

No `SectionName` constant — these are referenced from the parent config, not bound independently.

### 3. Create `RedisStoreOptions` class

**File: `ClientManager.Api/Models/Configuration/RedisStoreOptions.cs`**

Properties to include:
- `ConnectionString` (string, required)
- `UseTls` (bool, default `false`)
- `TlsCertificatePath` (string?, optional — path to PFX client certificate)
- `TlsCertificatePassword` (string?, optional)
- `AllowInsecureTls` (bool, default `false`)
- `ConnectTimeoutMilliseconds` (int, default `5000`)
- `SyncTimeoutMilliseconds` (int, default `5000`)
- `DatabaseIndex` (int, default `0` — Redis DB number)
- `Password` (string?, optional — used when `ConnectionString` doesn't embed the password)

### 4. Create `JsonFileStoreOptions` class

**File: `ClientManager.Api/Models/Configuration/JsonFileStoreOptions.cs`**

Properties to include:
- `DataDirectory` (string, default `"./data"`)
- `PrettyPrint` (bool, default `true`)

Keep it simple — JSON file is mainly for dev/single-instance.

### 5. Create `StorageRoleBinding` class

**File: `ClientManager.Api/Models/Configuration/StorageRoleBinding.cs`**

This class maps a single storage role to its provider. Properties:
- `Provider` (`PersistenceProvider` — which backend to use)
- `MongoDb` (`MongoDbStoreOptions?`) — populated when `Provider` is `MongoDb`
- `Redis` (`RedisStoreOptions?`) — populated when `Provider` is `Redis`
- `JsonFile` (`JsonFileStoreOptions?`) — populated when `Provider` is `JsonFile`

XML docs should explain that exactly one of the platform options should be non-null, matching the `Provider` value.

### 6. Redesign `PersistenceOptions`

**File: `ClientManager.Api/Models/Configuration/PersistenceOptions.cs`** (replace existing content)

New structure:
- `SectionName` stays `"Persistence"`
- `DefaultProvider` (`PersistenceProvider`, default `JsonFile`) — the fallback when a role has no explicit binding
- `DefaultMongoDb` (`MongoDbStoreOptions?`) — default platform settings for roles using MongoDb
- `DefaultRedis` (`RedisStoreOptions?`) — default platform settings for roles using Redis
- `DefaultJsonFile` (`JsonFileStoreOptions?`) — default platform settings for JsonFile
- `Roles` (`Dictionary<StorageRole, StorageRoleBinding>?`) — per-role overrides (optional)

**Design rationale**: The `Default*` properties let users configure a provider once and have it apply to all roles. The `Roles` dictionary allows selective overrides for specific roles. If `Roles` is null or a role is missing from it, that role falls back to `DefaultProvider` + the matching `Default*` options.

This design supports:
- **Simple case**: Just set `DefaultProvider` and one `Default*` block → same as today, one backend for everything
- **Mixed case**: Set `DefaultProvider: MongoDb` + `DefaultMongoDb: {...}` globally, then override `Roles.RateLimiting` to use Redis with its own connection string
- **Full control**: Every role has an explicit entry in `Roles`

### 7. Update `appsettings.json` example

Update the `Persistence` section to use the new schema:

```json
{
  "Persistence": {
    "DefaultProvider": "JsonFile",
    "DefaultJsonFile": {
      "DataDirectory": "./data"
    }
  }
}
```

## Verification

- Project compiles without errors (`dotnet build`)
- All new types are in the expected namespaces and files
- `PersistenceOptions` deserializes from both the simple (one provider) and mixed (per-role) JSON formats
- The old `PersistenceOptions` class is fully replaced — no legacy properties remain
- `ServiceCollectionExtensions` is updated to use the new `PersistenceOptions` shape (may require temporary adjustments to compile)
- **UI: Not affected by this step — configuration models are API-only**
