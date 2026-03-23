# Plan: Logarithmic Axis Scaling — Step 1: Foundation

> **Status**: ✅ Completed
> **Prerequisite**: None — this is the first step.
> **Next**: [logarithmic-scaling-2-settings-card.md](logarithmic-scaling-2-settings-card.md)
> **Parent**: [logarithmic-scaling-overview.md](logarithmic-scaling-overview.md)

## TL;DR

Add the `AxisScaleType` enum, a `LogarithmicScaleHelper` static utility class for transforming chart values + formatting axis labels, and extend `UserPreferences` / `UserPreferencesService` with a `DefaultAxisScale` property so the rest of the plan has a foundation to build on.

## Reference Pattern

In [ClientManager.AdminUI/Models/UserPreferences.cs](ClientManager.AdminUI/Models/UserPreferences.cs):
- Simple POCO with string-typed properties and sensible defaults (`"light"`, `"1h"`, `"10s"`)

In [ClientManager.AdminUI/Services/UserPreferencesService.cs](ClientManager.AdminUI/Services/UserPreferencesService.cs):
- `GetDefaultTimeRangeAsync()` / `GetDefaultPollingIntervalAsync()` pattern — read from cached prefs, resolve to a preset object, fall back to a `Default` static member

In [ClientManager.AdminUI/Models/TimeRangePreset.cs](ClientManager.AdminUI/Models/TimeRangePreset.cs):
- Static `All` list, `FindByKey()` helper, `Default` property

In [ClientManager.AdminUI/Services/ChartBucketAggregator.cs](ClientManager.AdminUI/Services/ChartBucketAggregator.cs):
- Static helper class with pure methods, inner records for inputs/outputs

## Steps

### 1. Create `AxisScaleType` enum

Create a new file `ClientManager.AdminUI/Models/AxisScaleType.cs`:

```csharp
namespace ClientManager.AdminUI.Models;

public enum AxisScaleType
{
    Linear,
    Logarithmic
}
```

### 2. Update `UserPreferences` model

In [ClientManager.AdminUI/Models/UserPreferences.cs](ClientManager.AdminUI/Models/UserPreferences.cs), add:

```csharp
public string DefaultAxisScale { get; set; } = "Linear";
```

### 3. Add helper methods to `UserPreferencesService`

In [ClientManager.AdminUI/Services/UserPreferencesService.cs](ClientManager.AdminUI/Services/UserPreferencesService.cs), add a method following the same pattern as `GetDefaultTimeRangeAsync()`:

```csharp
public async Task<AxisScaleType> GetDefaultAxisScaleAsync()
{
    var prefs = await GetPreferencesAsync();
    return Enum.TryParse<AxisScaleType>(prefs.DefaultAxisScale, out var scale)
        ? scale
        : AxisScaleType.Linear;
}
```

### 4. Create `LogarithmicScaleHelper` utility

Create a new file `ClientManager.AdminUI/Services/LogarithmicScaleHelper.cs`:

```csharp
namespace ClientManager.AdminUI.Services;

public static class LogarithmicScaleHelper
{
    /// Transforms a value for logarithmic display: log10(value + 1)
    public static double Transform(double value)
    {
        return Math.Log10(value + 1);
    }

    /// Inverse transform: 10^value - 1, used to recover original value from a transformed one
    public static double InverseTransform(double transformed)
    {
        return Math.Pow(10, transformed) - 1;
    }

    /// Formats an axis tick label — takes the transformed value, recovers the original, and displays it compactly
    public static string FormatAxisLabel(object transformedValue)
    {
        if (transformedValue is not double d) return "";
        var original = InverseTransform(d);
        return original switch
        {
            < 1 => original.ToString("F1"),
            < 1_000 => original.ToString("N0"),
            < 1_000_000 => (original / 1_000).ToString("N1") + "K",
            _ => (original / 1_000_000).ToString("N1") + "M"
        };
    }
}
```

## Verification

- `dotnet build ClientManager.AdminUI/ClientManager.AdminUI.csproj` compiles without errors
- `AxisScaleType.Linear` and `AxisScaleType.Logarithmic` are resolvable
- `LogarithmicScaleHelper.Transform(0)` returns `0`, `Transform(9)` returns `1.0`, `Transform(99)` returns `2.0`
- `LogarithmicScaleHelper.FormatAxisLabel(2.0)` returns `"99"` (or `"100"` after rounding)
- **UI: Navigate to the Dashboard (`/`) — verify no regressions; charts still render normally since nothing is wired up yet**
