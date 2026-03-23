# Plan: Structured NLog Logging Standardization — Step 2: API Configuration

> **Status**: ✅ Completed
> **Prerequisite**: [logging-standardization-1-foundation.md](logging-standardization-1-foundation.md)
> **Next**: [logging-standardization-3-adminui-config.md](logging-standardization-3-adminui-config.md)
> **Parent**: [logging-standardization-overview.md](logging-standardization-overview.md)

## TL;DR

Register `IAppLogger<T>` / `AppLogger<T>` in the API's DI container, update the NLog config to render `ExtraData.*` properties in both console and JSON layouts, and migrate the `Program.cs` bootstrap logging to use the new pattern.

## Reference Pattern

In [ClientManager.Api/Extensions/ServiceCollectionExtensions.cs](ClientManager.Api/Extensions/ServiceCollectionExtensions.cs):
- Services are registered via extension methods on `IServiceCollection`.
- The `AddClientManager` method delegates to helper methods like `RegisterApplicationServices`, `RegisterRepositories`, etc.

In [ClientManager.Api/Program.cs](ClientManager.Api/Program.cs):
- NLog is wired up via `LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger()`.
- Bootstrap logger uses `logger.Info(...)` and `logger.Error(...)` directly (NLog API).

In [ClientManager.Api/nlog.config](ClientManager.Api/nlog.config):
- JSON layout uses `includeEventProperties="true"` which will automatically render `ExtraData.*` properties.
- Console layout uses a `${logLayout}` variable that currently doesn't render event properties.

## Steps

### 1. Register `IAppLogger<T>` as open generic in DI

In [ClientManager.Api/Extensions/ServiceCollectionExtensions.cs](ClientManager.Api/Extensions/ServiceCollectionExtensions.cs), add to the `AddClientManager` method, near the top (before other registrations):

```csharp
using ClientManager.Shared.Logging;
```

Inside `AddClientManager`, add:

```csharp
services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));
```

This goes at the top of `AddClientManager`, before other registrations, so it's available to all services.

### 2. Update NLog console layout to include event properties

In [ClientManager.Api/nlog.config](ClientManager.Api/nlog.config), update the `logLayout` variable to append event properties:

**Before:**
```xml
<variable name="logLayout"
          value="${longdate}|${level:uppercase=true}|${activity:property=TraceId}|${logger}|${message}${onexception:inner= | ${exception:format=tostring}}" />
```

**After:**
```xml
<variable name="logLayout"
          value="${longdate}|${level:uppercase=true}|${activity:property=TraceId}|${logger}|${message}${onexception:inner= | ${exception:format=tostring}}${when:when=length('${all-event-properties}')>0:inner= | ${all-event-properties}}" />
```

This conditionally appends all event properties (including `ExtraData.*`) to the console output when they exist.

### 3. Update NLog rules to also capture AdminUI logs

In [ClientManager.Api/nlog.config](ClientManager.Api/nlog.config), update the logger rules to use `ClientManager.*` instead of `ClientManager.Api.*` so shared logging from `ClientManager.Shared.Logging.AppLogger` (which resolves logger names like `ClientManager.Api.Services.AccessControlService`) still matches:

The existing rules already match by the full type name, so no change needed here — the NLog logger name is `typeof(T).FullName` which will be `ClientManager.Api.Services.AccessControlService` etc. The rules `ClientManager.Api.*` will continue to match.

### 4. Migrate `Program.cs` bootstrap logging

In [ClientManager.Api/Program.cs](ClientManager.Api/Program.cs), the bootstrap logger uses NLog directly (not through DI). These 3 calls use the `{Url}` and `{DocsUrl}` template placeholders. Convert them to static messages with properties.

**Before (lines 73-79):**
```csharp
logger.Info("API listening on {Url}", url);
if (app.Environment.IsDevelopment())
{
    logger.Info("Swagger docs available at {DocsUrl}", $"{url}/docs");
}
```

**After:**
```csharp
var bootstrapLogEvent = new LogEventInfo(NLog.LogLevel.Info, logger.Name, "API listening");
bootstrapLogEvent.Properties["ExtraData.Url"] = url;
logger.Log(bootstrapLogEvent);

if (app.Environment.IsDevelopment())
{
    var docsLogEvent = new LogEventInfo(NLog.LogLevel.Info, logger.Name, "Swagger docs available");
    docsLogEvent.Properties["ExtraData.DocsUrl"] = $"{url}/docs";
    logger.Log(docsLogEvent);
}
```

**Before (line 86):**
```csharp
logger.Error(exception, "Stopped program because of exception");
```

**After:**
```csharp
var errorEvent = new LogEventInfo(NLog.LogLevel.Error, logger.Name, "Stopped program because of exception");
errorEvent.Exception = exception;
logger.Log(errorEvent);
```

Note: The bootstrap logger in `Program.cs` is obtained before DI is built, so it uses NLog directly. We use `LogEventInfo` with `Properties` to match the `ExtraData.*` convention. This is the only place that bypasses `IAppLogger<T>`.

## Verification

- API project compiles without errors.
- Run the API and check console output — messages should appear with `ExtraData.*` properties appended when present.
- The JSON file logs at `logs/clientmanager-*.log` should include `ExtraData.*` properties as top-level JSON fields (already supported via `includeEventProperties="true"`).
- **UI: Start the API, navigate to Swagger at `/docs` — confirm it loads without errors.**
- **UI: Navigate to any AdminUI page that calls the API — confirm no regressions.**
