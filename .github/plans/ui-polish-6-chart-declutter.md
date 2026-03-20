# Plan: UI Polish — Step 6: Chart Declutter for Many Clients

> **Status**: ✅ Completed
> **Prerequisite**: [ui-polish-5-dashboard-stacked-chart.md](ui-polish-5-dashboard-stacked-chart.md)
> **Next**: None — this is the final step.
> **Parent**: [ui-polish-overview.md](ui-polish-overview.md)

## TL;DR

When client count exceeds a threshold (10), charts and donut become unreadable. Add a "top N + Others" aggregation strategy that shows the top 10 clients by value individually and rolls all remaining clients into a single gray "Others" series. Apply this to stacked area charts on Dashboard, Monitor, and Allocations, and to the Dashboard donut chart.

## Reference Pattern

The per-client data construction already happens in each page's `LoadDataAsync` / `LoadChartDataAsync`. The aggregation should be applied as a post-processing step on the `List<ClientAreaSeries>` before it's assigned to `_targetCharts`.

In [Monitor.razor](../../ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor), the per-service branch builds `clientAreas`:
```csharp
clientAreas.Add(new ClientAreaSeries(entry.ClientName, points));
```

After all clients are collected, an aggregation step should run before building `TargetChartData`.

## Steps

### 1. Add `ChartAggregator` helper

Create `ClientManager.AdminUI/Services/ChartAggregator.cs`:

```csharp
namespace ClientManager.AdminUI.Services;

/// <summary>
/// Aggregates chart series data to keep visualizations readable when
/// there are many entities.
/// </summary>
public static class ChartAggregator
{
    public const int DefaultTopN = 10;
    public const string OthersId = "__others__";
    public const string OthersLabel = "Others";

    /// <summary>
    /// Takes a list of named series and returns at most topN individual
    /// series plus one "Others" series aggregating the rest.
    /// Series are ranked by total value (sum of all points).
    /// </summary>
    public static List<T> AggregateTopN<T>(
        List<T> series,
        Func<T, string> getId,
        Func<T, string> getName,
        Func<T, List<TPoint>> getPoints,
        Func<string, string, List<TPoint>, T> create,
        int topN = DefaultTopN)
        where TPoint allows any point type
    {
        // ... see detailed shape below
    }
}
```

**Detailed shape** — use a generic approach. Since the chart point types differ per page (`ChartPoint`, `TimeSeriesPoint`), the method should work with a common interface. Simplest approach: make it work on `List<(string Label, double Value)>` tuples internally.

A cleaner implementation approach:

```csharp
public static class ChartAggregator
{
    public const int DefaultTopN = 10;
    public const string OthersId = "__others__";
    public const string OthersLabel = "Others";

    /// <summary>
    /// Given a list of (id, name, points-as-list-of-label-value) tuples,
    /// returns at most topN + 1 (Others) entries.
    /// </summary>
    public static List<AggregatedSeries> Aggregate(
        List<AggregatedSeries> series,
        int topN = DefaultTopN)
    {
        if (series.Count <= topN)
            return series;

        var ranked = series
            .OrderByDescending(s => s.Points.Sum(p => p.Value))
            .ToList();

        var top = ranked.Take(topN).ToList();
        var rest = ranked.Skip(topN).ToList();

        // Merge rest into "Others"
        var allLabels = rest
            .SelectMany(s => s.Points.Select(p => p.Label))
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        var othersPoints = allLabels.Select(label => new AggregatedPoint(
            label,
            rest.Sum(s => s.Points.FirstOrDefault(p => p.Label == label)?.Value ?? 0)
        )).ToList();

        top.Add(new AggregatedSeries(OthersId, OthersLabel, othersPoints));
        return top;
    }

    public record AggregatedPoint(string Label, double Value);
    public record AggregatedSeries(string Id, string Name, List<AggregatedPoint> Points);
}
```

### 2. Apply aggregation in Monitor

In `ClientManager.AdminUI/Components/Pages/Monitor/Monitor.razor`, after building `clientAreas` for a service, convert to `AggregatedSeries`, call `ChartAggregator.Aggregate()`, then convert back to `ClientAreaSeries` (or adapt the page to work directly with `AggregatedSeries` — either approach works, keep it consistent with the page's existing patterns).

### 3. Apply aggregation in ActiveAllocations

Same pattern as step 2, applied in `ClientManager.AdminUI/Components/Pages/Allocations/ActiveAllocations.razor` after building per-client areas for a pool.

### 4. Apply aggregation in Dashboard stacked area chart

Same pattern, applied in `ClientManager.AdminUI/Components/Pages/Dashboard.razor` after building per-client chart series in both `LoadSingleTargetChartDataAsync` and `LoadAllTargetsChartDataAsync`.

### 5. Apply aggregation in Dashboard donut chart

After building `_perClientUsage`, if the list exceeds `DefaultTopN`, keep the top N entries and aggregate the rest into an "Others" entry:

```csharp
if (_perClientUsage.Count > ChartAggregator.DefaultTopN)
{
    var top = _perClientUsage
        .OrderByDescending(p => p.Value)
        .Take(ChartAggregator.DefaultTopN)
        .ToList();
    var othersValue = _perClientUsage
        .OrderByDescending(p => p.Value)
        .Skip(ChartAggregator.DefaultTopN)
        .Sum(p => p.Value);
    top.Add(new ClientUsagePoint(ChartAggregator.OthersId, ChartAggregator.OthersLabel, othersValue));
    _perClientUsage = top;
}
```

### 6. Set "Others" color in `EntityColorService`

Ensure the "Others" aggregate always gets a neutral gray color. In `EntityColorService`, add:

```csharp
public string GetColor(string entityId)
{
    if (entityId == "__others__")
        return "#94a3b8"; // neutral slate gray
    // ... existing logic
}
```

## Verification

- Project compiles without errors
- **UI: Seed the system with 15+ clients using the traffic generator or seed script. Navigate to `/monitor` with a specific service — verify only 10 client areas + "Others" appear in the stacked chart legend**
- **UI: Navigate to `/` (Dashboard) — verify the donut chart shows at most 10 slices + "Others"**
- **UI: Navigate to `/allocations` — verify the same aggregation appears**
- **UI: With fewer than 10 clients, verify no "Others" series appears — all clients shown individually**
- **UI: Verify the "Others" series uses the neutral gray color, not a palette color**
