# Plan: Timed Statistics — Step 1: Foundation

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [timed-statistics-2-data-layer.md](timed-statistics-2-data-layer.md)
> **Parent**: [timed-statistics-overview.md](timed-statistics-overview.md)

## TL;DR

Define the shared entity models, enums, response DTOs, and configuration model needed for historical usage tracking. These are the types that all subsequent steps depend on.

## Reference Pattern

In [ClientManager.Shared/Models/Entities/ResourceAllocation.cs](ClientManager.Shared/Models/Entities/ResourceAllocation.cs):
- Record types with `{ get; init; }` properties and XML doc comments
- Lives under `ClientManager.Shared.Models.Entities` namespace

In [ClientManager.Api/Models/Responses/UsageTimeSeriesResponse.cs](ClientManager.Api/Models/Responses/UsageTimeSeriesResponse.cs):
- Positional record types for response DTOs
- Lives under `ClientManager.Api.Models.Responses` namespace

In [ClientManager.Api/Models/PersistenceOptions.cs](ClientManager.Api/Models/PersistenceOptions.cs):
- Options class with `const string SectionName` for config binding
- Default values on properties
- Lives under `ClientManager.Api.Models` namespace

## Steps

### 1. Create `BucketGranularity` enum

File: `ClientManager.Shared/Models/Enums/BucketGranularity.cs`

```csharp
namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Defines the time granularity for usage tracking buckets.
/// </summary>
public enum BucketGranularity
{
    FiveMinute,
    Hour,
    Day
}
```

### 2. Create `UsageEventType` enum

File: `ClientManager.Shared/Models/Enums/UsageEventType.cs`

```csharp
namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Defines the type of usage event being tracked.
/// </summary>
public enum UsageEventType
{
    /// <summary>Request granted or resource acquired.</summary>
    Granted,
    /// <summary>Request denied or resource acquisition denied.</summary>
    Denied
}
```

### 3. Create `UsageTargetType` enum (if not already covered)

Check if the existing `GlobalRateLimitTarget` enum can be reused. If it has `Service` and `ResourcePool` values, reuse it. Otherwise create a separate enum. The existing enum is in `ClientManager.Shared/Models/Enums/GlobalRateLimitTarget.cs` and likely has these values — reuse it as the `targetType` field.

### 4. Create `UsageBucket` model

File: `ClientManager.Shared/Models/Entities/UsageBucket.cs`

```csharp
namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// A single time bucket containing granted and denied counts for a usage metric.
/// </summary>
public record UsageBucket
{
    /// <summary>UTC start of this time bucket.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Number of granted requests or successful acquisitions in this bucket.</summary>
    public long GrantedCount { get; init; }

    /// <summary>Number of denied requests or denied acquisitions in this bucket.</summary>
    public long DeniedCount { get; init; }
}
```

### 5. Create `UsageSnapshot` entity

File: `ClientManager.Shared/Models/Entities/UsageSnapshot.cs`

```csharp
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Stores the time-series usage data for a single client-target combination at a specific granularity.
/// Each document contains an ordered list of time-bucketed counters.
/// </summary>
public record UsageSnapshot
{
    /// <summary>
    /// Compound key: "{ClientId}:{TargetType}:{TargetId}:{Granularity}"
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>ID of the client this usage data belongs to.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>ID of the service or resource pool being tracked.</summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>Whether the target is a Service or ResourcePool.</summary>
    public GlobalRateLimitTarget TargetType { get; init; }

    /// <summary>The time granularity of the buckets in this snapshot.</summary>
    public BucketGranularity Granularity { get; init; }

    /// <summary>Ordered list of usage buckets, oldest first.</summary>
    public List<UsageBucket> Buckets { get; init; } = new();
}
```

### 6. Create `UsageTrackingOptions` configuration model

File: `ClientManager.Api/Models/UsageTrackingOptions.cs`

```csharp
namespace ClientManager.Api.Models;

/// <summary>
/// Configuration options for historical usage tracking.
/// </summary>
public class UsageTrackingOptions
{
    public const string SectionName = "UsageTracking";

    /// <summary>How often the in-memory buffer is flushed to persistent storage, in seconds. Default: 300 (5 minutes).</summary>
    public int FlushIntervalSeconds { get; set; } = 300;

    /// <summary>How long to retain 5-minute granularity buckets, in hours. Default: 24.</summary>
    public int FiveMinuteRetentionHours { get; set; } = 24;

    /// <summary>How long to retain hourly granularity buckets, in days. Default: 7.</summary>
    public int HourlyRetentionDays { get; set; } = 7;

    /// <summary>How long to retain daily granularity buckets, in days. Default: 90.</summary>
    public int DailyRetentionDays { get; set; } = 90;
}
```

### 7. Create response DTOs for historical usage queries

File: `ClientManager.Api/Models/Responses/HistoricalUsageResponse.cs`

```csharp
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Response containing historical usage time-series data for a target.
/// </summary>
public record HistoricalUsageResponse(
    string TargetId,
    string TargetType,
    string Granularity,
    List<HistoricalUsagePoint> Points);

/// <summary>
/// A single data point in the historical usage time-series.
/// </summary>
public record HistoricalUsagePoint(
    DateTime Timestamp,
    long GrantedCount,
    long DeniedCount);
```

## Verification

- Solution compiles without errors after adding all new files
- New enums are accessible from both `ClientManager.Shared` and `ClientManager.Api` projects
- New DTOs follow the same record pattern as existing response types
- `UsageTrackingOptions.SectionName` matches the config section name used in later steps
