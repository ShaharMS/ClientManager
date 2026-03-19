# Plan: Timed Statistics — Step 5: Background Service

> **Status**: 🔲 Not started
> **Prerequisite**: [timed-statistics-4-event-integration.md](timed-statistics-4-event-integration.md)
> **Next**: [timed-statistics-6-api-endpoints.md](timed-statistics-6-api-endpoints.md)
> **Parent**: [timed-statistics-overview.md](timed-statistics-overview.md)

## TL;DR

Create the `UsagePersistenceService` background service that runs on a configurable interval (default: 5 minutes). Each tick it: (1) drains the in-memory buffer, (2) writes the counts into 5-minute `UsageSnapshot` documents, (3) rolls up 5-minute buckets into hourly buckets and hourly into daily, and (4) prunes buckets older than their retention period.

## Reference Pattern

In [ClientManager.Api/Services/AllocationCleanupService.cs](ClientManager.Api/Services/AllocationCleanupService.cs):
- Extends `BackgroundService`
- Uses `IServiceScopeFactory` to resolve scoped/singleton services
- Loops with `Task.Delay` on a configurable interval
- Logs errors but continues running
- Registered via `services.AddHostedService<T>()` in `RegisterBackgroundServices`

## Steps

### 1. Create `UsagePersistenceService`

File: `ClientManager.Api/Services/UsageTracking/UsagePersistenceService.cs`

The service must handle three responsibilities per tick:

**a) Flush buffer to 5-minute snapshots**

```csharp
// Drain the buffer atomically
var counts = _buffer.Drain();
var bucketTimestamp = RoundDownToFiveMinutes(DateTime.UtcNow);

// Group by (clientId, targetType, targetId) and merge granted+denied into a single bucket
// For each group:
//   1. Load existing UsageSnapshot from repo (or create new)
//   2. Find or create UsageBucket for bucketTimestamp
//   3. Add granted/denied counts to the bucket
//   4. Upsert back to repo
```

Helper to compute bucket timestamp:
```csharp
private static DateTime RoundDownToFiveMinutes(DateTime utc)
{
    return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 5 * 5, 0, DateTimeKind.Utc);
}
```

**b) Roll up older 5-minute buckets into hourly**

```csharp
// Load all FiveMinute snapshots
// For each snapshot, find buckets older than 1 hour that haven't been rolled up yet
// Aggregate them into the corresponding Hour snapshot:
//   - hourBucketTimestamp = RoundDownToHour(bucket.Timestamp)
//   - Sum GrantedCount and DeniedCount into the hourly bucket
// Remove the rolled-up 5-minute buckets from the FiveMinute snapshot
```

**c) Roll up hourly buckets into daily (same pattern)**

```csharp
// Load all Hour snapshots
// For each, find buckets older than 24 hours
// Aggregate into corresponding Day snapshot
// Remove rolled-up hourly buckets
```

**d) Prune expired buckets**

```csharp
// FiveMinute: remove buckets older than FiveMinuteRetentionHours (default 24h)
// Hour: remove buckets older than HourlyRetentionDays (default 7d)
// Day: remove buckets older than DailyRetentionDays (default 90d)
// If a snapshot has no remaining buckets, delete the document
```

### 2. Structure of `UsagePersistenceService`

```csharp
public class UsagePersistenceService : BackgroundService
{
    private readonly ILogger<UsagePersistenceService> _logger;
    private readonly UsageBuffer _buffer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UsageTrackingOptions _options;

    // Constructor receives logger, buffer (singleton), scopeFactory, IOptions<UsageTrackingOptions>

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.FlushIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider
                    .GetRequiredService<IUsageSnapshotRepository>();

                await FlushBufferAsync(repository, stoppingToken);
                await RollUpAsync(repository, stoppingToken);
                await PruneExpiredAsync(repository, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in usage persistence cycle");
            }
        }
    }
}
```

### 3. Register in DI

In [ClientManager.Api/Extensions/ServiceCollectionExtensions.cs](ClientManager.Api/Extensions/ServiceCollectionExtensions.cs):

**Bind configuration** (in `AddClientManager`):
```csharp
services.Configure<UsageTrackingOptions>(
    configuration.GetSection(UsageTrackingOptions.SectionName));
```

**Register background service** (in `RegisterBackgroundServices`):
```csharp
services.AddHostedService<UsagePersistenceService>();
```

### 4. Add default config section to appsettings.json

In [ClientManager.Api/appsettings.json](ClientManager.Api/appsettings.json), add:

```json
"UsageTracking": {
    "FlushIntervalSeconds": 300,
    "FiveMinuteRetentionHours": 24,
    "HourlyRetentionDays": 7,
    "DailyRetentionDays": 90
}
```

## Verification

- Solution compiles without errors
- After the API runs for >5 minutes with traffic, `UsageSnapshots` collection contains documents in the data store
- 5-minute bucket timestamps are correctly rounded (e.g., 14:07 → 14:05)
- After 1+ hours, hourly rollup documents appear and 5-minute buckets older than 1 hour are removed
- Expired buckets are pruned according to retention settings
- Service logs errors but does not crash on transient failures
- Service respects cancellation token for clean shutdown
