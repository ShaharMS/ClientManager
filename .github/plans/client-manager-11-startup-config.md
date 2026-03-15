# Plan: ClientManager — Step 11: Startup, DI Wiring & Configuration

> **Status**: 🔲 Not started
> **Prerequisite**: [client-manager-10-statistics-endpoints.md](client-manager-10-statistics-endpoints.md)
> **Next**: [client-manager-12-admin-ui-foundation.md](client-manager-12-admin-ui-foundation.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Wire up all dependency injection registrations, configure `appsettings.json` with the full configuration schema, set up provider selection based on config, register the background cleanup service, configure NLog logging, middleware pipeline (RequestTracking → ErrorHandling), OpenTelemetry metrics with Prometheus exporter, statistics services, and seed initial data from config if provided. This is the glue step that makes everything work together.

## Reference Pattern

In [ClientManager.Api/Program.cs](ClientManager.Api/Program.cs):
- Standard ASP.NET Core 9.0 minimal hosting with `AddControllers()`, `AddOpenApi()`, and controller mapping
- Configuration via `appsettings.json` / `appsettings.Development.json`

## Steps

### 1. Define the full configuration schema in `appsettings.json`

**File: `ClientManager.Api/appsettings.json`** (replace existing content):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "Elasticsearch": {
      "Uri": "http://localhost:9200"
    }
  },
  "AllowedHosts": "*",
  "Persistence": {
    "Provider": "JsonFile",
    "JsonFileDataDirectory": "./data",
    "MongoDbConnectionString": null,
    "MongoDbDatabaseName": "ClientManager",
    "RedisConnectionString": null
  },
  "Seed": {
    "ClientConfigurations": [],
    "Services": [],
    "ResourcePools": [],
    "GlobalRateLimits": []
  }
}
```

**File: `ClientManager.Api/appsettings.Development.json`** (update with dev-friendly defaults):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Persistence": {
    "Provider": "JsonFile",
    "JsonFileDataDirectory": "./data"
  }
}
```

### 2. Create the seed configuration model

**File: `ClientManager.Api/Models/SeedOptions.cs`**

```csharp
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Models;

public class SeedOptions
{
    public const string SectionName = "Seed";

    public List<ClientConfiguration> ClientConfigurations { get; set; } = [];
    public List<Service> Services { get; set; } = [];
    public List<ResourcePool> ResourcePools { get; set; } = [];
    public List<GlobalRateLimit> GlobalRateLimits { get; set; } = [];
}
```

### 3. Create a DI registration extension

**File: `ClientManager.Api/Extensions/ServiceCollectionExtensions.cs`**

Create an extension method `AddClientManager(this IServiceCollection services, IConfiguration configuration)` that:

1. Binds `PersistenceOptions` from config
2. Based on `PersistenceOptions.Provider`, registers the appropriate `ClientManager.DataAccess` implementations:

**JsonFile provider:**
```csharp
services.AddSingleton<IClientConfigurationRepository>(sp =>
    new JsonFileClientConfigurationRepository(dataDir));
services.AddSingleton<IEntityRepository<Service>>(sp =>
    new JsonFileRepository<Service>(dataDir, s => s.Id));
services.AddSingleton<IEntityRepository<ResourcePool>>(sp =>
    new JsonFileRepository<ResourcePool>(dataDir, r => r.Id));
services.AddSingleton<IGlobalRateLimitRepository, JsonFileGlobalRateLimitRepository>();
services.AddSingleton<IRateLimitStateStore, JsonFileRateLimitStateStore>();
services.AddSingleton<IResourceAllocationRepository, JsonFileResourceAllocationRepository>();
```

**MongoDB provider:**
```csharp
services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(options.MongoDbConnectionString));
services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(options.MongoDbDatabaseName));
services.AddSingleton<IClientConfigurationRepository>(sp =>
    new MongoDbClientConfigurationRepository(
        sp.GetRequiredService<IMongoDatabase>().GetCollection<ClientConfiguration>("client_configurations")));
services.AddSingleton<IEntityRepository<Service>>(sp =>
    new MongoDbRepository<Service>(
        sp.GetRequiredService<IMongoDatabase>().GetCollection<Service>("services")));
// ... same pattern for other repositories
```

**Redis provider:**
```csharp
services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(options.RedisConnectionString!));
services.AddSingleton<IClientConfigurationRepository, RedisClientConfigurationRepository>();
// ... other repositories using IConnectionMultiplexer
```

3. Register services (provider-agnostic):
```csharp
services.AddSingleton<FixedWindowStrategy>();
services.AddSingleton<SlidingWindowStrategy>();
services.AddSingleton<TokenBucketStrategy>();
services.AddSingleton<RateLimitStrategyResolver>();
services.AddScoped<IRateLimitService, RateLimitService>();
services.AddScoped<IResourceAllocationService, ResourceAllocationService>();
services.AddScoped<IAccessControlService, AccessControlService>();
```

4. Register the background cleanup service:
```csharp
services.AddHostedService<AllocationCleanupService>();
```

### 4. Create a data seeding hosted service

**File: `ClientManager.Api/Services/DataSeedService.cs`**

An `IHostedService` (not `BackgroundService`) that runs once at startup:

```csharp
public class DataSeedService : IHostedService
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly SeedOptions _seedOptions;
    private readonly IServiceScopeFactory _scopeFactory;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Seed ClientConfigurations via IClientConfigurationRepository
        // Seed Services via IEntityRepository<Service>
        // Seed ResourcePools via IEntityRepository<ResourcePool>
        // Seed GlobalRateLimits via IGlobalRateLimitRepository
        // For each: check if entity already exists (by ID), create if not, log what was seeded
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Register in the extension method:
```csharp
var seedOptions = configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>();
if (seedOptions is not null)
{
    services.AddSingleton(seedOptions);
    services.AddHostedService<DataSeedService>();
}
```

### 5. Update `Program.cs`

**File: `ClientManager.Api/Program.cs`**

Replace the existing content with:

```csharp
using ClientManager.Api.Extensions;
using ClientManager.Api.Middleware;
using ClientManager.Api.Services.Instrumentation;
using NLog;
using NLog.Web;
using System.Text.Json.Serialization;

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Logging
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Controllers
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    builder.Services.AddOpenApi();

    // Application services, persistence, rate limiting, etc.
    builder.Services.AddClientManager(builder.Configuration);

    // OpenTelemetry metrics + Prometheus
    builder.Services.AddSingleton<ClientManagerMetrics>();
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddMeter(ClientManagerMetrics.MeterName);
            metrics.AddPrometheusExporter();
        });

    var app = builder.Build();

    // Middleware pipeline — order matters
    app.UseMiddleware<RequestTrackingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    app.MapPrometheusScrapingEndpoint("/metrics");

    app.Run();
}
catch (Exception exception)
{
    logger.Error(exception, "Stopped program because of exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}
```

### 6. Add `./data` to `.gitignore`

**File: `ClientManager.Api/.gitignore`** (create or append):

```
data/
```

This prevents the JSON file persistence directory from being committed.

## Verification

- `dotnet build` succeeds with no errors
- Application starts with `dotnet run` using default `JsonFile` config
- NLog produces console output and creates log files in `logs/` directory
- `./data/` directory is created on first write
- Seed data from `appsettings.json` is loaded on startup (if configured)
- Seed data uses `ClientConfigurations` (full nested documents) instead of separate client/access-rule/policy lists
- Switching `Persistence.Provider` to `"MongoDb"` or `"Redis"` with valid connection strings works
- `RequestTrackingMiddleware` logs request start/completion with trace IDs and duration
- `ErrorHandlingMiddleware` maps exceptions to proper HTTP status codes and problem details
- All API endpoints are functional end-to-end (test with `ClientManager.http` or Swagger)
- Background cleanup service starts and runs without errors
- GET `/metrics` returns Prometheus-format metrics
- OpenAPI spec is available at `/openapi/v1.json` in development
- Middleware pipeline order: RequestTracking → ErrorHandling → Controllers
