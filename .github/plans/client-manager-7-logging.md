# Plan: ClientManager — Step 7: Logging Infrastructure

> **Status**: ✅ Completed
> **Prerequisite**: [client-manager-6-api-endpoints.md](client-manager-6-api-endpoints.md)
> **Next**: [client-manager-8-statistics-collection.md](client-manager-8-statistics-collection.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Set up NLog as the logging provider with structured logging conventions. Configure three targets (Console, File, Elasticsearch) via `nlog.config`. Establish patterns for using `NLog.LogManager.GetCurrentClassLogger()` with structured properties, `System.Diagnostics.Activity` for correlation IDs, and `NLog.ScopeContext` for scoped properties. All services created in previous steps already reference `NLog.Logger` — this step creates the configuration and conventions that make those calls produce useful output.

## Reference Pattern

NLog with ASP.NET Core follows the standard `NLog.Web.AspNetCore` integration pattern:

- [NLog ASP.NET Core Wiki](https://github.com/NLog/NLog/wiki/Getting-started-with-ASP.NET-Core-6)
- Logger obtained as `private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();`
- Structured logging via message templates: `Logger.Info("Message | {@Properties}", new { Key = value })`
- Layout renderers like `${activity:property=TraceId}` pick up `Activity.Current` automatically

## Steps

### 1. Add NLog NuGet packages

**File: `ClientManager.Api/ClientManager.Api.csproj`**

```xml
<PackageReference Include="NLog" Version="5.*" />
<PackageReference Include="NLog.Web.AspNetCore" Version="5.*" />
<PackageReference Include="NLog.Targets.ElasticSearch" Version="8.*" />
```

### 2. Create `nlog.config`

**File: `ClientManager.Api/nlog.config`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      autoReload="true"
      throwConfigExceptions="true">

  <extensions>
    <add assembly="NLog.Web.AspNetCore" />
    <add assembly="NLog.Targets.ElasticSearch" />
  </extensions>

  <variable name="logLayout"
            value="${longdate}|${level:uppercase=true}|${activity:property=TraceId}|${logger}|${message}${onexception:inner= | ${exception:format=tostring}}" />

  <variable name="jsonLayout">
    <layout type="JsonLayout" includeEventProperties="true">
      <attribute name="timestamp" layout="${longdate}" />
      <attribute name="level" layout="${level:uppercase=true}" />
      <attribute name="traceId" layout="${activity:property=TraceId}" />
      <attribute name="spanId" layout="${activity:property=SpanId}" />
      <attribute name="logger" layout="${logger}" />
      <attribute name="message" layout="${message:raw=true}" />
      <attribute name="exception" layout="${exception:format=tostring}" />
    </layout>
  </variable>

  <targets async="true">
    <!-- Console: plain text for local development -->
    <target name="console"
            xsi:type="Console"
            layout="${logLayout}" />

    <!-- File: JSON for structured log ingestion -->
    <target name="file"
            xsi:type="File"
            fileName="${basedir}/logs/clientmanager-${shortdate}.log"
            layout="${jsonLayout}"
            archiveEvery="Day"
            maxArchiveFiles="14"
            concurrentWrites="true" />

    <!-- Elasticsearch: structured JSON directly to Elastic -->
    <target name="elastic"
            xsi:type="ElasticSearch"
            index="clientmanager-logs-${date:format=yyyy.MM.dd}"
            uri="${configsetting:item=Logging.Elasticsearch.Uri}"
            requireAuth="false"
            includeAllProperties="true">
      <field name="traceId" layout="${activity:property=TraceId}" />
      <field name="spanId" layout="${activity:property=SpanId}" />
      <field name="logger" layout="${logger}" />
      <field name="machineName" layout="${machinename}" />
    </target>
  </targets>

  <rules>
    <!-- Skip noisy Microsoft logs below Warning -->
    <logger name="Microsoft.*" maxlevel="Info" final="true" />
    <logger name="System.Net.Http.*" maxlevel="Info" final="true" />

    <!-- All application logs: Console + File -->
    <logger name="ClientManager.Api.*" minlevel="Debug" writeTo="console,file" />

    <!-- Elasticsearch: Info and above only -->
    <logger name="ClientManager.Api.*" minlevel="Info" writeTo="elastic" />
  </rules>
</nlog>
```

Set the file to copy to output directory:

**File: `ClientManager.Api/ClientManager.Api.csproj`**

```xml
<ItemGroup>
  <Content Include="nlog.config" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### 3. Configure NLog in `Program.cs`

**File: `ClientManager.Api/Program.cs`**

Add NLog setup before `builder.Build()`:

```csharp
using NLog;
using NLog.Web;

// Early init NLog to capture startup errors
var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // ... existing service registration ...

    var app = builder.Build();
    // ... middleware pipeline ...
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

### 4. Add Elasticsearch URI to `appsettings.json`

**File: `ClientManager.Api/appsettings.json`**

```json
{
  "Logging": {
    "Elasticsearch": {
      "Uri": "http://localhost:9200"
    }
  }
}
```

The Elasticsearch target reads this via `${configsetting:item=Logging.Elasticsearch.Uri}` in `nlog.config`. If the URI is empty or Elasticsearch is unreachable, NLog silently skips the target — no code changes needed.

### 5. Document the structured logging convention

All services in the project follow this pattern:

```csharp
// Logger declaration — one per class, static
private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

// Structured logging — static message + anonymous object with properties
Logger.Info("Access granted | {@Properties}", new { ClientId = clientId, ServiceId = serviceId });

// With exception
Logger.Error(exception, "Failed to acquire resource | {@Properties}", new { ClientId = clientId, ResourcePoolId = poolId });

// Debug level for high-frequency operations
Logger.Debug("Rate limit evaluated | {@Properties}", new { ClientId = clientId, ServiceId = serviceId, Allowed = result.IsAllowed, Remaining = result.RemainingRequests });
```

**Conventions:**
- Static message portion describes the event in human-readable form
- All dynamic data goes into `{@Properties}` as an anonymous object
- Use `Debug` for high-frequency service internals (rate limit evaluations)
- Use `Info` for significant business events (access granted, resource acquired/released)
- Use `Warn` for degraded-but-functional states (approaching rate limits, pool nearly full)
- Use `Error` for caught exceptions and error states
- Never use string interpolation in log messages — always structured parameters

### 6. Activity and correlation ID support

`NLog.Web.AspNetCore` automatically picks up `Activity.Current` from the ASP.NET Core request pipeline. No manual Activity creation is needed for HTTP requests — ASP.NET Core creates one per request by default.

The `nlog.config` layout already uses `${activity:property=TraceId}` and `${activity:property=SpanId}`, so all log entries within a request automatically include the trace/span correlation.

For the `AllocationCleanupService` (background service), there is no HTTP request. Start a manual activity:

```csharp
using var activity = new Activity("AllocationCleanup").Start();
Logger.Info("Cleanup started | {@Properties}", new { Timestamp = DateTime.UtcNow });
// ... cleanup work ...
Logger.Info("Cleanup completed | {@Properties}", new { CleanedUp = count, Duration = activity.Duration.TotalMilliseconds });
```

## Verification

- `dotnet build` succeeds with NLog packages installed
- Application starts without NLog configuration errors
- Console output shows structured log lines with trace IDs during HTTP requests
- Log files are created in `logs/` directory with JSON format
- Elasticsearch target is configured but does not crash when Elastic is unreachable
- All services from steps 3–5 produce structured log output via `NLog.Logger`
- Background service logs include Activity correlation IDs
