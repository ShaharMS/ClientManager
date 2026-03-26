# Plan: Multi-Provider Storage Architecture — Step 4: Startup Validation & Config Examples

> **Status**: 🔲 Not started
> **Prerequisite**: [multi-provider-storage-3-database-migration.md](multi-provider-storage-3-database-migration.md)
> **Next**: None — this is the final step of the multi-provider plan. The [search-query-layer-overview.md](search-query-layer-overview.md) plan depends on this plan being complete.
> **Parent**: [multi-provider-storage-overview.md](multi-provider-storage-overview.md)

## TL;DR

Add startup validation that catches misconfigured storage roles early (missing connection strings, invalid provider values), log the resolved provider for each role at startup, and provide well-documented `appsettings.json` examples showing both simple and mixed-provider configurations.

## Reference Pattern

In [ClientManager.Api/appsettings.json](ClientManager.Api/appsettings.json):
- Current flat `Persistence` section — will be replaced entirely with the new schema

In [ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs](ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs):
- `AddClientManager` reads `PersistenceOptions` at startup — validation logic goes here

## Steps

### 1. Add startup validation

**File: `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs`**

After binding `PersistenceOptions`, validate before registering stores:

1. `DefaultProvider` must be a valid enum value
2. For each `StorageRole`, resolve its effective binding (explicit role entry or default). For each resolved binding:
   - If `Provider` is `MongoDb`, the `MongoDb` options (or `DefaultMongoDb`) must have a non-empty `ConnectionString`
   - If `Provider` is `Redis`, the `Redis` options (or `DefaultRedis`) must have a non-empty `ConnectionString`
   - If `Provider` is `JsonFile`, the `JsonFile` options (or `DefaultJsonFile`) must have a non-empty `DataDirectory`
3. If validation fails, throw a clear `InvalidOperationException` with a message naming the role and what's missing

Example error message:
```
Storage configuration invalid: role 'RateLimiting' is configured to use Redis, but no Redis ConnectionString was provided. Set either Persistence:Roles:RateLimiting:Redis:ConnectionString or Persistence:DefaultRedis:ConnectionString.
```

### 2. Log the resolved storage configuration at startup

**File: `ClientManager.Api/Utils/Extensions/ServiceCollectionExtensions.cs`**

After registration, log (at `Info` level) which provider each role resolved to. Example output:
```
Storage role Configuration → MongoDb (mongodb://localhost:27017)
Storage role RateLimiting → Redis (localhost:6379)
Storage role Allocations → MongoDb (mongodb://localhost:27017)
Storage role Statistics → MongoDb (mongodb://analytics-host:27017)
```

Mask passwords/secrets in the log output — only show host:port or a truncated connection string.

### 3. Update `appsettings.json` with new config schema

**File: `ClientManager.Api/appsettings.json`**

Replace the `Persistence` section entirely:
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

### 4. Add mixed-provider example in `appsettings.Development.json`

**File: `ClientManager.Api/appsettings.Development.json`**

Add a mixed-provider example showing the full capabilities:

```json
{
  "Persistence": {
    "DefaultProvider": "MongoDb",
    "DefaultMongoDb": {
      "ConnectionString": "mongodb://localhost:27017",
      "DatabaseName": "ClientManager",
      "UseTls": false
    },
    "Roles": {
      "RateLimiting": {
        "Provider": "Redis",
        "Redis": {
          "ConnectionString": "localhost:6379",
          "DatabaseIndex": 0
        }
      },
      "Statistics": {
        "Provider": "MongoDb",
        "MongoDb": {
          "ConnectionString": "mongodb://analytics-host:27017",
          "DatabaseName": "ClientManagerStats"
        }
      }
    }
  }
}
```

### 5. Remove the old `PersistenceOptions` flat properties

**File: `ClientManager.Api/Models/Configuration/PersistenceOptions.cs`**

Ensure no remnants of the old flat properties (`Provider`, `JsonFileDataDirectory`, `MongoDbConnectionString`, `MongoDbDatabaseName`, `RedisConnectionString`) exist. The class should only contain the new-schema properties established in sub-plan 1.

## Verification

- Project compiles without errors
- Application starts with the new simple config format — works correctly
- Application starts with a mixed config (if Mongo/Redis are available in dev) — each role resolves to its own store
- Missing connection string for a configured provider triggers a clear startup exception naming the role and what's missing
- Startup logs show the resolved provider for each role with masked connection strings
- **UI: Navigate to the dashboard — verify everything loads with the default JsonFile config**
- **UI: Navigate to Clients, Services, Resource Pools pages — verify all data renders correctly**
- **UI: Open the time-series charts — verify usage data still works**
