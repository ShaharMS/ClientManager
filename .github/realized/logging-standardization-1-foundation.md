# Plan: Structured NLog Logging Standardization — Step 1: Foundation

> **Status**: ✅ Completed
> **Prerequisite**: None — this is the first step.
> **Next**: [logging-standardization-2-api-config.md](logging-standardization-2-api-config.md)
> **Parent**: [logging-standardization-overview.md](logging-standardization-overview.md)

## TL;DR

Create the `IAppLogger<T>` interface and `AppLogger<T>` implementation in `ClientManager.Shared`. This is the single logging abstraction that both the API and AdminUI will use. It enforces static messages, optional exceptions, and optional extra data objects whose properties become NLog event properties prefixed with `ExtraData.`.

## Reference Pattern

The existing pattern in [ClientManager.Api/Services/AccessControlService.cs](ClientManager.Api/Services/AccessControlService.cs) shows `ILogger<T>` injection via constructor:
```csharp
private readonly ILogger<AccessControlService> _logger;
// ...
_logger.LogInformation("Access granted | ClientId={ClientId}, ServiceId={ServiceId}",
    clientId, serviceId);
```

The new pattern replaces this with:
```csharp
private readonly IAppLogger<AccessControlService> _logger;
// ...
_logger.Info("Access granted", new { ClientId = clientId, ServiceId = serviceId });
```

## Steps

### 1. Add NLog package reference to `ClientManager.Shared`

In [ClientManager.Shared/ClientManager.Shared.csproj](ClientManager.Shared/ClientManager.Shared.csproj), add:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.*" />
  <PackageReference Include="NLog" Version="5.*" />
</ItemGroup>
```

`Microsoft.Extensions.Logging.Abstractions` provides `ILogger<T>` without pulling in the full hosting stack. `NLog` is needed for `LogEventInfo` construction in `AppLogger<T>`.

### 2. Create the `IAppLogger<T>` interface

Create file `ClientManager.Shared/Logging/IAppLogger.cs`:

```csharp
namespace ClientManager.Shared.Logging;

/// <summary>
/// Structured logging interface that enforces static messages with optional exception and extra data.
/// </summary>
/// <typeparam name="T">The category type for the logger.</typeparam>
public interface IAppLogger<T>
{
    void Trace(string message);
    void Trace(string message, object extraData);
    void Trace(string message, Exception exception, object? extraData = null);

    void Debug(string message);
    void Debug(string message, object extraData);
    void Debug(string message, Exception exception, object? extraData = null);

    void Info(string message);
    void Info(string message, object extraData);
    void Info(string message, Exception exception, object? extraData = null);

    void Warn(string message);
    void Warn(string message, object extraData);
    void Warn(string message, Exception exception, object? extraData = null);

    void Error(string message);
    void Error(string message, object extraData);
    void Error(string message, Exception exception, object? extraData = null);

    void Fatal(string message);
    void Fatal(string message, object extraData);
    void Fatal(string message, Exception exception, object? extraData = null);
}
```

Three overloads per level:
- `(string message)` — message only, no dynamic data
- `(string message, object extraData)` — message + extra data, no exception
- `(string message, Exception exception, object? extraData = null)` — message + exception + optional extra data

C# overload resolution handles `Exception` vs `object` correctly: calling with an `Exception` argument resolves to the `Exception` overload (more specific), while anonymous objects resolve to the `object` overload.

### 3. Create the `AppLogger<T>` implementation

Create file `ClientManager.Shared/Logging/AppLogger.cs`:

```csharp
using System.Reflection;
using Microsoft.Extensions.Logging;
using NLog;
using LogLevel = NLog.LogLevel;

namespace ClientManager.Shared.Logging;

/// <summary>
/// NLog-backed implementation of <see cref="IAppLogger{T}"/> that attaches extra data
/// properties with an <c>ExtraData.</c> prefix to each log event.
/// </summary>
/// <typeparam name="T">The category type for the logger.</typeparam>
public class AppLogger<T> : IAppLogger<T>
{
    private readonly NLog.Logger _nlogLogger;

    public AppLogger(ILogger<T> logger)
    {
        // Resolve the NLog logger using the same category name as ILogger<T>
        _nlogLogger = LogManager.GetLogger(typeof(T).FullName);
    }

    public void Trace(string message) => Log(LogLevel.Trace, message, null, null);
    public void Trace(string message, object extraData) => Log(LogLevel.Trace, message, null, extraData);
    public void Trace(string message, Exception exception, object? extraData = null) => Log(LogLevel.Trace, message, exception, extraData);

    public void Debug(string message) => Log(LogLevel.Debug, message, null, null);
    public void Debug(string message, object extraData) => Log(LogLevel.Debug, message, null, extraData);
    public void Debug(string message, Exception exception, object? extraData = null) => Log(LogLevel.Debug, message, exception, extraData);

    public void Info(string message) => Log(LogLevel.Info, message, null, null);
    public void Info(string message, object extraData) => Log(LogLevel.Info, message, null, extraData);
    public void Info(string message, Exception exception, object? extraData = null) => Log(LogLevel.Info, message, exception, extraData);

    public void Warn(string message) => Log(LogLevel.Warn, message, null, null);
    public void Warn(string message, object extraData) => Log(LogLevel.Warn, message, null, extraData);
    public void Warn(string message, Exception exception, object? extraData = null) => Log(LogLevel.Warn, message, exception, extraData);

    public void Error(string message) => Log(LogLevel.Error, message, null, null);
    public void Error(string message, object extraData) => Log(LogLevel.Error, message, null, extraData);
    public void Error(string message, Exception exception, object? extraData = null) => Log(LogLevel.Error, message, exception, extraData);

    public void Fatal(string message) => Log(LogLevel.Fatal, message, null, null);
    public void Fatal(string message, object extraData) => Log(LogLevel.Fatal, message, null, extraData);
    public void Fatal(string message, Exception exception, object? extraData = null) => Log(LogLevel.Fatal, message, exception, extraData);

    private void Log(LogLevel level, string message, Exception? exception, object? extraData)
    {
        if (!_nlogLogger.IsEnabled(level))
            return;

        var logEvent = new LogEventInfo(level, _nlogLogger.Name, message)
        {
            Exception = exception
        };

        if (extraData is not null)
        {
            foreach (var property in extraData.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = property.GetValue(extraData);
                logEvent.Properties[$"ExtraData.{property.Name}"] = value;
            }
        }

        _nlogLogger.Log(logEvent);
    }
}
```

Key design points:
- Resolves the NLog `Logger` by `typeof(T).FullName` so the NLog rules (`ClientManager.Api.*`, etc.) still match.
- The `ILogger<T>` constructor parameter is accepted to stay compatible with Microsoft DI but the actual logging goes through NLog directly.
- Extra data properties are attached with the `ExtraData.` prefix so they appear as `ExtraData.ClientId`, `ExtraData.ServiceId`, etc. in structured output.
- Early-exit via `IsEnabled` check avoids reflection overhead when the level is suppressed.

## Verification

- `ClientManager.Shared` compiles without errors after adding the two new files and NuGet references.
- `IAppLogger<T>` exposes six log levels, each with three overloads: `(message)`, `(message, extraData)`, `(message, exception, extraData?)`.
- `AppLogger<T>` can be instantiated with an `ILogger<T>` parameter.
- No other projects are affected yet — this step is purely additive.
