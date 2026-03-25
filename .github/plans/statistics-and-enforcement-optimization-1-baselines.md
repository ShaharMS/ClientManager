# Plan: Statistics and Enforcement Performance Optimization — Step 1: Baselines and Guardrails

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [statistics-and-enforcement-optimization-2-enforcement-hot-path.md](statistics-and-enforcement-optimization-2-enforcement-hot-path.md)
> **Parent**: [statistics-and-enforcement-optimization-overview.md](statistics-and-enforcement-optimization-overview.md)

## TL;DR

Add measurement and rollout controls before touching the hot path or usage storage. This step makes later optimizations measurable, reversible, and safe to compare against the current behavior.

## Reference Pattern

[../../ClientManager.Api/Services/Instrumentation/ClientManagerMetrics.cs](../../ClientManager.Api/Services/Instrumentation/ClientManagerMetrics.cs) shows how shared counters and histograms are registered once and injected across services.

In [../../ClientManager.Api/Services/Instrumentation/ClientManagerMetrics.cs](../../ClientManager.Api/Services/Instrumentation/ClientManagerMetrics.cs):
- Add new instruments in the same central meter rather than scattering ad-hoc metrics in individual services.
- Keep metric naming consistent with the existing `clientmanager.*` namespace.

[../../ClientManager.Api/Services/StatisticsService.cs](../../ClientManager.Api/Services/StatisticsService.cs) shows the current short-lived caching pattern that can be extended behind configuration.

In [../../ClientManager.Api/Services/StatisticsService.cs](../../ClientManager.Api/Services/StatisticsService.cs):
- Use small, explicit cache durations and named cache keys.
- Keep cache behavior isolated behind service methods instead of leaking it into controllers.

## Steps

### 1. Add optimization options and rollout flags

Create a dedicated options type under `ClientManager.Api/Models` for performance toggles and bind it in `ClientManager.Api/Extensions/ServiceCollectionExtensions.cs`. Include flags for the major later changes so the implementation can be rolled out incrementally.

```csharp
public sealed class PerformanceOptimizationOptions
{
    public bool EnableEnforcementContextReuse { get; set; }
    public bool EnableSegmentedUsageSnapshots { get; set; }
}
```

Update `ClientManager.Api/appsettings.json` with disabled-by-default flags unless an optimization is already risk-free.

### 2. Add latency and volume instrumentation around the actual bottlenecks

Extend `ClientManager.Api/Services/Instrumentation/ClientManagerMetrics.cs` and instrument these services:

- `ClientManager.Api/Services/AccessControlService.cs`
- `ClientManager.Api/Services/ResourceAllocationService.cs`
- `ClientManager.Api/Services/RateLimiting/RateLimitService.cs`
- `ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs`
- `ClientManager.Api/Services/StatisticsService.cs`

Capture at least:

- Access-check duration and resource-acquire duration.
- Rate-limit strategy duration, split by strategy name.
- Usage flush, roll-up, and prune durations plus bucket counts processed.
- Statistics query duration and snapshots/buckets touched per request.

Prefer histograms for timings and counters for touched-document counts.

### 3. Create a repeatable benchmark and storage-size baseline

Use `_scripts/traffic_generator.py` as the starting point for a deterministic load profile that approximates the user’s scale assumptions: about 100 clients, 20 services/resource pools, and 1M requests/day equivalent traffic. Record the before-state for:

- Access-check throughput and P95 latency.
- Resource acquire/release throughput and P95 latency.
- Dashboard and monitor endpoint latency.
- On-disk `UsageSnapshots` and `_counters` size for the Json provider.

Keep the benchmark code in scripts or test helpers, not in markdown notes, so later steps can reuse it directly.

## Verification

- The API project builds without errors.
- The new options bind successfully from configuration and default to current behavior when disabled.
- Running the benchmark/load script produces baseline timings and storage-size numbers without changing access or allocation outcomes.
- UI: Navigate to `/` and verify dashboard cards and charts still load without error alerts.
- UI: Navigate to `/monitor`, change the service filter, and verify charts/table still refresh with data.
- UI: Navigate to `/allocations`, change the pool filter, and verify the page still renders active allocation data without layout or loading regressions.