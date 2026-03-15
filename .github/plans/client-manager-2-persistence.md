# Plan: ClientManager — Step 2: Persistence Layer

> **Status**: ✅ Completed
> **Prerequisite**: [client-manager-1-foundation.md](client-manager-1-foundation.md)
> **Next**: [client-manager-3-rate-limiting.md](client-manager-3-rate-limiting.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Implement the persistence layer with three providers: JSON file (for development), MongoDB, and Redis. All implementations live in `ClientManager.DataAccess/Implementations/` and implement the interfaces defined in Step 1. All entity types are `record` with `init` properties — `System.Text.Json`, `MongoDB.Driver`, and `StackExchange.Redis` all support `init`-only setters natively. This covers the client configuration repository, system-wide entity repositories (Service, ResourcePool, GlobalRateLimit), and operational state stores (rate limit counters, resource allocations). When entities need updating (e.g. `ResourceAllocation.IsReleased`), use `with` expressions to create modified copies.

## Reference Pattern

No existing reference in this project. Standard repository pattern with provider-specific implementations in separate folders.

Target folder structure:
```
ClientManager.DataAccess/
  Implementations/
    JsonFile/
    MongoDb/
    Redis/
```

## Steps

### 1. Add NuGet packages

Add the following packages to `ClientManager.DataAccess.csproj`:
```xml
<PackageReference Include="MongoDB.Driver" Version="3.*" />
<PackageReference Include="StackExchange.Redis" Version="2.*" />
```

### 2. Create JSON file persistence provider

The JSON file provider stores each entity collection in a separate JSON file in a configurable directory (default: `./data/`). It uses file locking for thread safety. This is intended for development use only and single-instance deployments.

**File: `ClientManager.DataAccess/Implementations/JsonFile/JsonFileRepository.cs`**

Implement `IEntityRepository<T>` generically by:
- Storing entities as a `List<T>` serialized to `{dataDirectory}/{typeof(T).Name}.json`
- Using `SemaphoreSlim` for thread-safe file access
- Using `System.Text.Json` for serialization
- Creating the data directory and file on first write if they don't exist
- Using a `Func<T, string>` id selector passed via constructor

```csharp
public JsonFileRepository(string dataDirectory, Func<T, string> idSelector)
```

**File: `ClientManager.DataAccess/Implementations/JsonFile/JsonFileClientConfigurationRepository.cs`**

Implements `IClientConfigurationRepository`:
- Extends `JsonFileRepository<ClientConfiguration>` for the base CRUD operations, using `c => c.Id` as the ID selector
- Sub-document methods (`GetServiceSettingsAsync`, `SetServiceSettingsAsync`, `RemoveServiceSettingsAsync`, etc.) load the full `ClientConfiguration`, modify the nested dictionary, and save back
- File path: `{dataDirectory}/ClientConfiguration.json`

```csharp
public class JsonFileClientConfigurationRepository : JsonFileRepository<ClientConfiguration>, IClientConfigurationRepository
{
    public JsonFileClientConfigurationRepository(string dataDirectory)
        : base(dataDirectory, c => c.Id) { }

    public async Task<ServiceAccessSettings?> GetServiceSettingsAsync(string clientId, string serviceId, ...) { ... }
    public async Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, ...) { ... }
    public async Task RemoveServiceSettingsAsync(string clientId, string serviceId, ...) { ... }
    public async Task<ResourcePoolSettings?> GetResourcePoolSettingsAsync(string clientId, string resourcePoolId, ...) { ... }
    public async Task SetResourcePoolSettingsAsync(string clientId, string resourcePoolId, ResourcePoolSettings settings, ...) { ... }
    public async Task RemoveResourcePoolSettingsAsync(string clientId, string resourcePoolId, ...) { ... }
}
```

**File: `ClientManager.DataAccess/Implementations/JsonFile/JsonFileGlobalRateLimitRepository.cs`**

Extends `JsonFileRepository<GlobalRateLimit>`, implements `IGlobalRateLimitRepository`.
- `GetByTargetAsync` — filters by `TargetId` and `TargetType`
- `GetByTargetTypeAsync` — filters by `TargetType`

**File: `ClientManager.DataAccess/Implementations/JsonFile/JsonFileRateLimitStateStore.cs`**

Implements `IRateLimitStateStore` using a JSON file `{dataDirectory}/RateLimitState.json` that stores a dictionary of key → `{ Count, WindowStart }`. Simple implementation for dev use:
- `IncrementAsync` — loads dict, checks if window expired (resets if so), increments, saves
- Uses `SemaphoreSlim` for thread safety

**File: `ClientManager.DataAccess/Implementations/JsonFile/JsonFileResourceAllocationRepository.cs`**

Implements `IResourceAllocationRepository` using a JSON file storing `List<ResourceAllocation>`:
- `GetActiveCountAsync` — counts allocations where `IsReleased == false` and `ExpiresAt > DateTime.UtcNow`
- `GetActiveCountByClientAsync` — same as above but also filters by `ClientId`
- `CleanupExpiredAsync` — marks expired unreleased allocations as released

### 3. Create MongoDB persistence provider

**File: `ClientManager.DataAccess/Implementations/MongoDb/MongoDbRepository.cs`**

Generic implementation of `IEntityRepository<T>`:
- Takes `IMongoCollection<T>` via constructor
- Uses `Id` property as the document `_id` (configure via `BsonClassMap` or attributes)
- Standard CRUD operations using MongoDB.Driver filter builders

```csharp
public MongoDbRepository(IMongoCollection<T> collection)
```

**File: `ClientManager.DataAccess/Implementations/MongoDb/MongoDbClientConfigurationRepository.cs`**

Implements `IClientConfigurationRepository`:
- Extends `MongoDbRepository<ClientConfiguration>` for base CRUD
- Sub-document methods use MongoDB update operators for efficient partial updates:
  - `SetServiceSettingsAsync` uses `UpdateOneAsync` with `$set` on `Services.{serviceId}`
  - `RemoveServiceSettingsAsync` uses `UpdateOneAsync` with `$unset` on `Services.{serviceId}`
  - `SetResourcePoolSettingsAsync` uses `$set` on `ResourcePools.{poolId}`
  - `RemoveResourcePoolSettingsAsync` uses `$unset` on `ResourcePools.{poolId}`
- `GetServiceSettingsAsync` loads the full document, then reads from the dictionary (MongoDB doesn't support sub-document projection into a C# dictionary value easily)

```csharp
public class MongoDbClientConfigurationRepository : MongoDbRepository<ClientConfiguration>, IClientConfigurationRepository
```

**File: `ClientManager.DataAccess/Implementations/MongoDb/MongoDbGlobalRateLimitRepository.cs`**

Implements `IGlobalRateLimitRepository`:
- `GetByTargetAsync` — filter builder on `TargetId` + `TargetType`
- `GetByTargetTypeAsync` — filter builder on `TargetType`
- Compound index on `{ TargetId, TargetType }`

**File: `ClientManager.DataAccess/Implementations/MongoDb/MongoDbRateLimitStateStore.cs`**

Implements `IRateLimitStateStore`:
- Uses a dedicated `rate_limit_state` collection with documents shaped as `{ _id: key, Count: long, WindowStart: DateTime }`
- `IncrementAsync` uses `FindOneAndUpdateAsync` with `$inc` for atomic increment, and checks window expiry
- TTL index on `WindowStart` for auto-cleanup

**File: `ClientManager.DataAccess/Implementations/MongoDb/MongoDbResourceAllocationRepository.cs`**

Implements `IResourceAllocationRepository`:
- `GetActiveCountAsync` — filter: `IsReleased == false && ExpiresAt > DateTime.UtcNow`, use `CountDocumentsAsync`
- `GetActiveCountByClientAsync` — same filter plus `ClientId` match
- `CreateAsync` — uses `InsertOneAsync`
- `MarkReleasedAsync` — uses `UpdateOneAsync` with `$set: { IsReleased: true }`
- `CleanupExpiredAsync` — uses `UpdateManyAsync` to mark expired allocations as released

### 4. Create Redis persistence provider

**File: `ClientManager.DataAccess/Implementations/Redis/RedisRepository.cs`**

Generic implementation of `IEntityRepository<T>`:
- Stores entities as JSON strings in Redis hashes: key = `entity:{TypeName}`, field = entity ID
- Uses `System.Text.Json` for serialization
- Takes `IConnectionMultiplexer` and a `Func<T, string>` id selector

**File: `ClientManager.DataAccess/Implementations/Redis/RedisClientConfigurationRepository.cs`**

Implements `IClientConfigurationRepository`:
- Extends `RedisRepository<ClientConfiguration>` for base CRUD (hash key: `entity:ClientConfiguration`)
- Sub-document methods load the full `ClientConfiguration` JSON from the hash, deserialize, modify the dictionary, re-serialize, and save back
- Uses `HashSetAsync` / `HashGetAsync` on the `entity:ClientConfiguration` hash with the client ID as the field

**File: `ClientManager.DataAccess/Implementations/Redis/RedisGlobalRateLimitRepository.cs`**

Implements `IGlobalRateLimitRepository`:
- Uses hash key `entity:GlobalRateLimit`
- For `GetByTargetAsync`, scans hash values and filters by `TargetId` + `TargetType`
- For `GetByTargetTypeAsync`, scans hash values and filters by `TargetType`

**File: `ClientManager.DataAccess/Implementations/Redis/RedisRateLimitStateStore.cs`**

Implements `IRateLimitStateStore`:
- `IncrementAsync` — uses Redis `INCR` on key `ratelimit:{key}` with `EXPIRE` set to the window duration. This is the ideal provider for rate limiting.
- `GetCountAsync` — uses `GET`
- `ResetAsync` — uses `DEL`
- Atomic operations using Redis transactions or Lua scripts where needed

**File: `ClientManager.DataAccess/Implementations/Redis/RedisResourceAllocationRepository.cs`**

Implements `IResourceAllocationRepository`:
- Active allocations stored in a Redis hash: `allocations:{resourcePoolId}` with allocation ID as field
- `GetActiveCountAsync` — uses `HLEN` (only active, non-expired entries stored)
- `GetActiveCountByClientAsync` — scans hash values, deserializes, filters by `ClientId` and non-expired
- `CreateAsync` — uses `HSET` + sets allocation data with expiry tracking
- `MarkReleasedAsync` — uses `HDEL`
- `CleanupExpiredAsync` — scans allocations, removes expired ones
- Uses a sorted set `allocation_expiry:{resourcePoolId}` with score = expiry timestamp for efficient TTL cleanup

### 5. Create persistence configuration model

**File: `ClientManager.Api/Models/PersistenceOptions.cs`**

```csharp
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Models;

public class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public PersistenceProvider Provider { get; set; } = PersistenceProvider.JsonFile;
    public string JsonFileDataDirectory { get; set; } = "./data";
    public string? MongoDbConnectionString { get; set; }
    public string MongoDbDatabaseName { get; set; } = "ClientManager";
    public string? RedisConnectionString { get; set; }
}
```

## Verification

- `dotnet build` succeeds with no errors
- All repository interfaces from Step 1 have at least one concrete implementation
- `IClientConfigurationRepository` is implemented by all three providers with full sub-document support
- JSON file provider creates files in the configured directory when entities are written
- MongoDB provider connects and creates collections when configured
- Redis provider connects and stores/retrieves entities when configured
- Each provider can be swapped by changing configuration (actual DI wiring is in Step 7)
- `GetActiveCountByClientAsync` works correctly for per-client slot cap enforcement
