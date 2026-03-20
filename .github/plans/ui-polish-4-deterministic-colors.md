# Plan: UI Polish — Step 4: Deterministic Entity Colors

> **Status**: 🔲 Not started
> **Prerequisite**: [ui-polish-3-skeleton-dashboard-monitor.md](ui-polish-3-skeleton-dashboard-monitor.md)
> **Next**: [ui-polish-5-dashboard-stacked-chart.md](ui-polish-5-dashboard-stacked-chart.md)
> **Parent**: [ui-polish-overview.md](ui-polish-overview.md)

## TL;DR

Create an `EntityColorService` that maps entity IDs (clients, services, pools) to deterministic HSL colors via a stable hash. Register it as a singleton so the same client always gets the same color across Dashboard, Monitor, Allocations, and any future page — regardless of render order or app restart.

## Reference Pattern

Currently, Radzen charts auto-assign colors from their internal palette, which means the same client can be a different color on Dashboard vs Monitor. There is no color utility in the codebase today.

Service registration pattern follows existing services in [ClientManager.AdminUI/Services/](../../ClientManager.AdminUI/Services/) and they are registered in [ClientManager.AdminUI/Program.cs](../../ClientManager.AdminUI/Program.cs).

## Steps

### 1. Create `EntityColorService.cs`

Create `ClientManager.AdminUI/Services/EntityColorService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace ClientManager.AdminUI.Services;

/// <summary>
/// Generates deterministic colors for entity IDs using a stable hash.
/// The same ID always produces the same color across sessions and pages.
/// </summary>
public class EntityColorService
{
    // A hand-picked palette of 20 distinct, visually pleasant hues.
    // When the entity count exceeds the palette size, we fall back to
    // hash-based HSL generation.
    private static readonly string[] Palette =
    [
        "#6366f1", "#f59e0b", "#22c55e", "#ef4444", "#3b82f6",
        "#ec4899", "#14b8a6", "#f97316", "#8b5cf6", "#06b6d4",
        "#84cc16", "#e11d48", "#0ea5e9", "#d946ef", "#10b981",
        "#facc15", "#7c3aed", "#fb923c", "#2dd4bf", "#a855f7"
    ];

    /// <summary>
    /// Returns a deterministic color string for the given entity ID.
    /// </summary>
    public string GetColor(string entityId)
    {
        var hash = GetStableHash(entityId);
        var index = (int)(hash % (uint)Palette.Length);
        return Palette[index];
    }

    /// <summary>
    /// Returns a deterministic color for the entity at the given position
    /// in an ordered list. Uses palette first, then falls back to hash.
    /// Call this when you have a known ordered set and want maximally
    /// distinct adjacent colors.
    /// </summary>
    public string GetColorByIndex(string entityId, int index)
    {
        if (index < Palette.Length)
            return Palette[index];
        return GetColor(entityId);
    }

    /// <summary>
    /// Returns colors for a batch of entity IDs, maintaining order.
    /// </summary>
    public Dictionary<string, string> GetColors(IEnumerable<string> entityIds)
    {
        var result = new Dictionary<string, string>();
        var idx = 0;
        foreach (var id in entityIds)
        {
            if (!result.ContainsKey(id))
            {
                result[id] = GetColorByIndex(id, idx);
                idx++;
            }
        }
        return result;
    }

    private static uint GetStableHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToUInt32(bytes, 0);
    }
}
```

### 2. Register as singleton in `Program.cs`

In `ClientManager.AdminUI/Program.cs`, add:

```csharp
builder.Services.AddSingleton<EntityColorService>();
```

Place it next to the other service registrations (look for `builder.Services.AddScoped<ClientApiService>` etc.).

### 3. Wire colors into Monitor chart rendering

In `ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor`:

- Add `@inject EntityColorService Colors` at the top.
- Where `RadzenStackedAreaSeries` is rendered for each `clientArea`, pass the Fill/Stroke color:

```razor
@foreach (var clientArea in targetChart.ClientSeries)
{
    <RadzenStackedAreaSeries Data="@clientArea.Points"
        CategoryProperty="Label" ValueProperty="Value"
        Title="@clientArea.ClientName"
        Fill="@Colors.GetColor(clientArea.ClientId)"
        Stroke="@Colors.GetColor(clientArea.ClientId)" />
}
```

This requires adding `ClientId` to the `ClientAreaSeries` record:
```csharp
private record ClientAreaSeries(string ClientId, string ClientName, List<ChartPoint> Points);
```

Update all call sites that construct `ClientAreaSeries` to pass the client ID.

### 4. Wire colors into ActiveAllocations chart rendering

Same changes as step 3, but in `ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor`:

- Add `@inject EntityColorService Colors`
- Pass `Fill`/`Stroke` to `RadzenStackedAreaSeries` using `Colors.GetColor(clientArea.ClientId)`
- Add `ClientId` to the `ClientAreaSeries` record and update constructors

### 5. Wire colors into Dashboard donut chart

In `ClientManager.AdminUI/Components/Pages/Dashboard.razor`:

- Add `@inject EntityColorService Colors`
- For the `RadzenDonutSeries`, pass colors via the `Fills` property (an `IEnumerable<string>` of colors in the same order as the data):

```razor
<RadzenDonutSeries Data="@_perClientUsage" CategoryProperty="ClientName"
    ValueProperty="Value" Title="Per Client"
    Fills="@_perClientUsage.Select(p => Colors.GetColor(p.ClientId)).ToArray()" />
```

This requires adding `ClientId` to the `ClientUsagePoint` record:
```csharp
private record ClientUsagePoint(string ClientId, string ClientName, double Value);
```

Update all call sites building `_perClientUsage` to include the client ID.

## Verification

- Project compiles without errors
- **UI: Navigate to `/monitor` with a specific service selected — note the colors assigned to each client. Navigate to `/allocations` — verify the same clients have the same colors**
- **UI: Navigate to `/` (Dashboard) — verify the donut chart uses the same colors for the same clients as Monitor**
- **UI: Refresh the page — verify colors remain identical (deterministic)**
- **UI: Take screenshots of Monitor and Dashboard side-by-side to confirm color consistency**
