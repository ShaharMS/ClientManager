# Plan: Structured NLog Logging Standardization

## Status: ✅ All steps completed

## Overview

The codebase currently uses `Microsoft.Extensions.Logging` (`ILogger<T>`) with NLog as the backend in the API project, and no structured logging at all in the AdminUI project. Logging call sites mix structured parameters into message templates (e.g., `"Resource not found | Path={Path}, Detail={Detail}"`), which couples dynamic data into the message string and makes log querying inconsistent.

This plan introduces a custom `IAppLogger<T>` abstraction in `ClientManager.Shared` that enforces the pattern: **static message string + optional exception + optional anonymous object for extra data**. Extra data properties are attached as NLog event properties prefixed with `ExtraData.` (e.g., `ExtraData.ClientId`). Both the API and AdminUI projects will use NLog via this wrapper, with no string interpolation allowed in log messages. There are ~26 existing logging call sites to migrate plus 3 bootstrap calls in `Program.cs`.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [logging-standardization-1-foundation.md](.github/plans/logging-standardization-1-foundation.md) | Create `IAppLogger<T>` interface and `AppLogger<T>` implementation in `ClientManager.Shared` |
| 2 | [logging-standardization-2-api-config.md](.github/plans/logging-standardization-2-api-config.md) | Update API NLog config, register `IAppLogger<T>`, update `Program.cs` bootstrap logging |
| 3 | [logging-standardization-3-adminui-config.md](.github/plans/logging-standardization-3-adminui-config.md) | Add NLog packages to AdminUI, create `nlog.config`, wire up in `Program.cs` |
| 4 | [logging-standardization-4-migrate-callsites.md](.github/plans/logging-standardization-4-migrate-callsites.md) | Migrate all existing `ILogger<T>` call sites to `IAppLogger<T>` across middleware and services |

## Key Decisions

- **Abstraction lives in `ClientManager.Shared`** — Both API and AdminUI already reference Shared. The interface (`IAppLogger<T>`) and implementation (`AppLogger<T>`) go here. The Shared project gets an `NLog` package reference since `AppLogger<T>` needs to interact with NLog's `LogEventInfo` to attach `ExtraData.*` properties.
- **`IAppLogger<T>` wraps `ILogger<T>`** — The implementation receives an `ILogger<T>` via DI and creates NLog `LogEventInfo` objects to attach extra data as event properties with the `ExtraData.` prefix. This preserves compatibility with the existing NLog pipeline.
- **Method overloads instead of optional parameters** — Each of the six levels (`Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal`) has three overloads: `(string message)`, `(string message, object extraData)`, and `(string message, Exception exception, object? extraData = null)`. This avoids passing `null` — callers use the overload that matches their data. C# overload resolution handles `Exception` vs `object` correctly (more specific type wins).
- **No string interpolation** — Messages must be compile-time static strings. All dynamic values go into the `extraData` anonymous object. This is enforced by convention (no `{placeholder}` in message strings).
- **NLog config updates** — The JSON layout already has `includeEventProperties="true"`, so `ExtraData.*` properties will appear automatically. The plain-text console layout will be extended to render event properties.
- **AdminUI gets a minimal NLog config** — Console + file targets only (no Elasticsearch). Same `ExtraData.*` property rendering.
