# Plan: Structured NLog Logging Standardization — Step 3: AdminUI NLog Setup

> **Status**: ✅ Completed
> **Prerequisite**: [logging-standardization-2-api-config.md](logging-standardization-2-api-config.md)
> **Next**: [logging-standardization-4-migrate-callsites.md](logging-standardization-4-migrate-callsites.md)
> **Parent**: [logging-standardization-overview.md](logging-standardization-overview.md)

## TL;DR

Add NLog packages to the AdminUI project, create an `nlog.config` file, wire NLog into `Program.cs`, and register `IAppLogger<T>` in DI. This gives AdminUI the same structured logging infrastructure as the API, ready for future logging calls.

## Reference Pattern

In [ClientManager.Api/Program.cs](ClientManager.Api/Program.cs):
- NLog is configured with `builder.Logging.ClearProviders()` and `builder.Host.UseNLog()`.
- `LogManager.Shutdown()` is called in a `finally` block.

In [ClientManager.Api/nlog.config](ClientManager.Api/nlog.config):
- Defines console and file targets with layouts that include `ExtraData.*` event properties.
- Rules filter Microsoft/System.Net.Http noise.

In [ClientManager.Api/ClientManager.Api.csproj](ClientManager.Api/ClientManager.Api.csproj):
- NLog packages: `NLog`, `NLog.Web.AspNetCore`.
- Content item: `<Content Update="nlog.config" CopyToOutputDirectory="PreserveNewest" />`.

## Steps

### 1. Add NLog packages to AdminUI

In [ClientManager.AdminUI/ClientManager.AdminUI.csproj](ClientManager.AdminUI/ClientManager.AdminUI.csproj), add:

```xml
<ItemGroup>
  <PackageReference Include="NLog" Version="5.*" />
  <PackageReference Include="NLog.Web.AspNetCore" Version="5.*" />
</ItemGroup>

<ItemGroup>
  <Content Update="nlog.config" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### 2. Create `nlog.config` for AdminUI

Create file `ClientManager.AdminUI/nlog.config`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      autoReload="true"
      throwConfigExceptions="true">

  <extensions>
    <add assembly="NLog.Web.AspNetCore" />
  </extensions>

  <variable name="logLayout"
            value="${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:inner= | ${exception:format=tostring}}${when:when=length('${all-event-properties}')>0:inner= | ${all-event-properties}}" />

  <variable name="jsonLayout">
    <layout type="JsonLayout" includeEventProperties="true">
      <attribute name="timestamp" layout="${longdate}" />
      <attribute name="level" layout="${level:uppercase=true}" />
      <attribute name="logger" layout="${logger}" />
      <attribute name="message" layout="${message:raw=true}" />
      <attribute name="exception" layout="${exception:format=tostring}" />
    </layout>
  </variable>

  <targets async="true">
    <target name="console"
            xsi:type="Console"
            layout="${logLayout}" />

    <target name="file"
            xsi:type="File"
            fileName="${basedir}/logs/clientmanager-adminui-${shortdate}.log"
            layout="${jsonLayout}"
            archiveEvery="Day"
            maxArchiveFiles="14"
            concurrentWrites="true" />
  </targets>

  <rules>
    <logger name="Microsoft.*" maxlevel="Info" final="true" />
    <logger name="System.Net.Http.*" maxlevel="Info" final="true" />

    <logger name="ClientManager.*" minlevel="Debug" writeTo="console,file" />
  </rules>
</nlog>
```

This mirrors the API's config but without the Elasticsearch target (AdminUI is a Blazor SSR app, not a public API). The rule uses `ClientManager.*` to capture both `ClientManager.AdminUI.*` and `ClientManager.Shared.*` logger names.

### 3. Wire NLog into AdminUI `Program.cs`

In [ClientManager.AdminUI/Program.cs](ClientManager.AdminUI/Program.cs), add NLog setup:

**Add using statements at the top:**
```csharp
using ClientManager.Shared.Logging;
using NLog;
using NLog.Web;
```

**After `var builder = WebApplication.CreateBuilder(args);`, add:**
```csharp
builder.Logging.ClearProviders();
builder.Host.UseNLog();
```

**Register `IAppLogger<T>` in DI (before `var app = builder.Build();`):**
```csharp
builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));
```

**Wrap the entire app run in try/catch/finally (matching the API pattern):**
```csharp
var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
    // ... existing builder and app code ...
    app.Run();
}
catch (Exception exception)
{
    var errorEvent = new LogEventInfo(NLog.LogLevel.Error, logger.Name, "Stopped AdminUI because of exception");
    errorEvent.Exception = exception;
    logger.Log(errorEvent);
    throw;
}
finally
{
    LogManager.Shutdown();
}
```

### 4. Restore NuGet packages

Run `dotnet restore` for the AdminUI project to pull in NLog packages.

## Verification

- AdminUI project compiles without errors.
- Run the AdminUI and confirm NLog initializes (console output shows NLog-formatted log lines instead of default ASP.NET Core format).
- Check that `logs/clientmanager-adminui-*.log` is created in the output directory.
- **UI: Navigate to the AdminUI root page — confirm it loads without errors.**
- **UI: Navigate to a page that calls the API (e.g., Clients list) — confirm data still renders correctly.**
- **UI: Take a screenshot to confirm no layout breakage or error banners.**
