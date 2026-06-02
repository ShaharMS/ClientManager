# Plan: Code Slim-Down — Step 1: Shared Foundation

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [code-slimdown-2-dataaccess.md](code-slimdown-2-dataaccess.md)
> **Parent**: [code-slimdown-overview.md](code-slimdown-overview.md)

## TL;DR

Slim the `ClientManager.Shared` project: convert data-carrier classes to records, collapse the 18 `IAppLogger`/`AppLogger` overloads into one method per level using optional parameters, consolidate one-enum-per-file into grouped files, and template-ize `StorageApiRoutes`. This is the foundation every other project depends on, so it goes first. Breaking changes are allowed for 1.0.0, but keep public type/member names stable unless a rename removes real duplication. **Documentation stays as-is — do not trim docs.**

## Iteration Bootstrap

- **Iteration slug**: `code-slimdown`
- **Required evidence**: `dotnet build ClientManager.Shared` clean; full-solution build still clean (no broken references from enum file moves); `git diff --stat` shows net deletions for this step.
- **UI artifacts to verify**: After building the full stack, load the AdminUI Dashboard (`/`) and confirm pages render — Shared types flow into every layer, so a broken record/logger change surfaces as runtime errors in the UI.
- **Commit-splitting guidance**: Separate commits for (a) record conversions, (b) logger overload collapse, (c) enum consolidation, (d) route template-ization.

## Reference Pattern

No generic prior art exists for these transformations; mirror the conventions already in the codebase.

In [ClientManager.Shared/Models/Entities/ClientConfiguration.cs](ClientManager.Shared/Models/Entities/ClientConfiguration.cs):
- Entity models are already `record` types — use this as the target shape for `ProblemResponse` and `UserPreferences`.

In [ClientManager.Shared/Logging/AppLogger.cs](ClientManager.Shared/Logging/AppLogger.cs):
- All overloads funnel into a single private `Log(...)` method — that internal method already proves the consolidated shape; the public overloads are the redundancy to remove.

In [ClientManager.Shared/Models/Enums/](ClientManager.Shared/Models/Enums/):
- Each enum is one file with the same namespace — moving several into one file is a pure namespace-preserving merge.

## Steps

### 1. Convert `ProblemResponse` and `StorageProblemResponse` to records

In [ClientManager.Shared/Models/Problems/ProblemResponse.cs](ClientManager.Shared/Models/Problems/ProblemResponse.cs) and [ClientManager.Shared/Models/Problems/StorageProblemResponse.cs](ClientManager.Shared/Models/Problems/StorageProblemResponse.cs), replace the init-only-property classes with positional/init `record` declarations. Keep property names identical so JSON (de)serialization is unchanged.

```csharp
public record ProblemResponse { public string? Title { get; init; } /* ...keep names... */ }
```

Verify System.Text.Json round-trips identically (property names, nullability, casing). If any consumer mutates these after construction, keep `init` accessors rather than positional parameters.

### 2. Convert `UserPreferences` to a record

`UserPreferences` lives in the AdminUI project ([ClientManager.AdminUI/Models/UserPreferences.cs](ClientManager.AdminUI/Models/UserPreferences.cs)) — convert it to a record with `init` properties and default values, preserving names used in localStorage/JSON serialization. (Listed here because it is the same transformation; the executing agent may also defer this single file to step 8.)

### 3. Collapse `IAppLogger<T>` overloads

In [ClientManager.Shared/Logging/IAppLogger.cs](ClientManager.Shared/Logging/IAppLogger.cs), reduce each level's three overloads to one signature using optional parameters:

```csharp
void Info(string message, object? data = null, Exception? exception = null);
```

Repeat for every level (Trace/Debug/Info/Warn/Error/Fatal or whatever set exists). Optional-parameter defaults are source-compatible, so existing call sites do not change.

### 4. Collapse `AppLogger<T>` implementation

In [ClientManager.Shared/Logging/AppLogger.cs](ClientManager.Shared/Logging/AppLogger.cs), implement the reduced interface so every level is a one-line expression-bodied member dispatching to the existing internal `Log(...)`. Delete the now-unused overloads.

### 5. Consolidate enum files

Merge the single-enum files in [ClientManager.Shared/Models/Enums/](ClientManager.Shared/Models/Enums/) and [ClientManager.Shared/Models/Search/](ClientManager.Shared/Models/Search/) into grouped files within the **same namespaces** (e.g., `StorageEnums.cs`, `RateLimitEnums.cs`, `UsageEnums.cs`, `SearchEnums.cs`). Because the namespace is unchanged, no `using` updates are required. Delete the emptied files. **Preserve each enum's existing documentation verbatim** when moving it — this is a file merge, not a doc edit.

### 6. Template-ize `StorageApiRoutes`

In [ClientManager.Shared/Contracts/Storage/StorageApiRoutes.cs](ClientManager.Shared/Contracts/Storage/StorageApiRoutes.cs), introduce a private `Escape` helper and `const` route templates, then format with `string.Format`/interpolation. Keep every public method name and the exact produced route strings identical (these are the wire contract). **Keep the existing XML docs on each method.**

## Verification

- `dotnet build ClientManager.Shared` compiles with zero warnings/errors.
- Full-solution build (`dotnet build ClientManager.slnx`) compiles — confirms enum moves and record changes broke no references.
- A serialization sanity check: a `ProblemResponse` / `StorageProblemResponse` instance serializes to the same JSON shape as before (same property names/casing).
- `git diff --stat` shows a net line reduction for the step.
- **UI: Start the full stack per the Local Testing runbook, then open the AdminUI Dashboard (`/`) and one list page (e.g., `/services`). Verify pages load with data and no error banners — confirms `record`/logger changes did not break runtime serialization or logging.**
- **UI: Take a screenshot of the Dashboard to confirm no layout/rendering regressions.**
