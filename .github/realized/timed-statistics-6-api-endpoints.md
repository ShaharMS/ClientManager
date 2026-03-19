# Plan: Timed Statistics — Step 6: API Endpoints

> **Status**: ✅ Completed
> **Prerequisite**: [timed-statistics-5-background-service.md](timed-statistics-5-background-service.md)
> **Next**: None — this is the final step.
> **Parent**: [timed-statistics-overview.md](timed-statistics-overview.md)

## TL;DR

Add a new historical usage endpoint and update the existing `usage-timeseries` and `global-usage` endpoints to return real data instead of zeros. This is the consumer-facing layer that makes the collected data queryable.

## Reference Pattern

In [ClientManager.Api/Controllers/StatisticsController.cs](ClientManager.Api/Controllers/StatisticsController.cs):
- Thin controller: validates input, delegates to `IStatisticsService`
- XML doc comments on every action
- `[ProducesResponseType]` attributes for all response codes
- `[FromQuery]` for query parameters

In [ClientManager.Api/Services/StatisticsService.cs](ClientManager.Api/Services/StatisticsService.cs):
- Implements `IStatisticsService`
- Reads from repositories, computes aggregated responses
- Async methods with `CancellationToken`

## Steps

### 1. Add `IUsageSnapshotRepository` to `StatisticsService`

Edit [ClientManager.Api/Services/StatisticsService.cs](ClientManager.Api/Services/StatisticsService.cs):

Add `IUsageSnapshotRepository _usageSnapshotRepository` as a constructor dependency.

### 2. Add `GetHistoricalUsageAsync` method to `IStatisticsService`

Edit [ClientManager.Api/Interfaces/IStatisticsService.cs](ClientManager.Api/Interfaces/IStatisticsService.cs):

```csharp
/// <summary>
/// Retrieves historical usage data for a specific target, optionally filtered by client.
/// </summary>
Task<HistoricalUsageResponse> GetHistoricalUsageAsync(
    string filterType,
    string targetId,
    string? clientId,
    DateTime from,
    DateTime to,
    BucketGranularity granularity,
    CancellationToken cancellationToken = default);
```

### 3. Implement `GetHistoricalUsageAsync` in `StatisticsService`

```csharp
public async Task<HistoricalUsageResponse> GetHistoricalUsageAsync(
    string filterType, string targetId, string? clientId,
    DateTime from, DateTime to, BucketGranularity granularity,
    CancellationToken cancellationToken = default)
{
    var targetType = ParseTargetType(filterType);

    IReadOnlyList<UsageSnapshot> snapshots;

    if (clientId is not null)
    {
        // Single client: load their specific snapshot
        var snapshot = await _usageSnapshotRepository.GetByClientAndTargetAsync(
            clientId, targetId, targetType, granularity, cancellationToken);
        snapshots = snapshot is not null ? [snapshot] : [];
    }
    else
    {
        // All clients: load all snapshots for this target and aggregate
        snapshots = await _usageSnapshotRepository.GetByTargetAsync(
            targetId, targetType, granularity, cancellationToken);
    }

    // Merge all snapshots' buckets into aggregated time points
    var aggregated = new SortedDictionary<DateTime, (long granted, long denied)>();

    foreach (var snapshot in snapshots)
    {
        foreach (var bucket in snapshot.Buckets)
        {
            if (bucket.Timestamp < from || bucket.Timestamp > to)
                continue;

            if (aggregated.TryGetValue(bucket.Timestamp, out var existing))
            {
                aggregated[bucket.Timestamp] = (
                    existing.granted + bucket.GrantedCount,
                    existing.denied + bucket.DeniedCount);
            }
            else
            {
                aggregated[bucket.Timestamp] = (bucket.GrantedCount, bucket.DeniedCount);
            }
        }
    }

    var points = aggregated.Select(kvp =>
        new HistoricalUsagePoint(kvp.Key, kvp.Value.granted, kvp.Value.denied)).ToList();

    return new HistoricalUsageResponse(targetId, filterType, granularity.ToString(), points);
}
```

### 4. Update `GetUsageTimeSeriesAsync` to use real data

Replace the zero-filled stub in `StatisticsService.GetUsageTimeSeriesAsync` with real data:

- Load 5-minute snapshots for the target from the last hour
- Aggregate across all clients (or filtered clients if `clientIds` is provided)
- Return the aggregated data as `UsagePoints` alongside the existing `CapPoints` logic
- Fall back to zero-filled points for time slots with no data (to keep a continuous series)

### 5. Update `GetGlobalUsageStatsAsync` to compute `RequestsPerMinute`

Replace the hardcoded `RequestsPerMinute: 0` in `StatisticsService.GetGlobalUsageStatsAsync`:

- Load all 5-minute `Service`-type snapshots
- Sum the `GrantedCount` from the most recent complete 5-minute bucket
- Divide by 5 to get requests per minute

```csharp
// Replace: RequestsPerMinute: 0
// With:
var latestBucketTime = RoundDownToFiveMinutes(DateTime.UtcNow).AddMinutes(-5);
var allServiceSnapshots = await _usageSnapshotRepository
    .GetAllByGranularityAsync(BucketGranularity.FiveMinute, cancellationToken);

var recentRequests = allServiceSnapshots
    .Where(s => s.TargetType == GlobalRateLimitTarget.Service)
    .SelectMany(s => s.Buckets)
    .Where(b => b.Timestamp == latestBucketTime)
    .Sum(b => b.GrantedCount);

var requestsPerMinute = Math.Round(recentRequests / 5.0, 1);
```

### 6. Add new endpoint to `StatisticsController`

Edit [ClientManager.Api/Controllers/StatisticsController.cs](ClientManager.Api/Controllers/StatisticsController.cs):

```csharp
/// <summary>
/// Retrieves historical usage data for a service or resource pool over a time range.
/// </summary>
/// <param name="filterType">Either "Service" or "ResourcePool".</param>
/// <param name="targetId">The ID of the service or resource pool.</param>
/// <param name="clientId">Optional: filter to a single client.</param>
/// <param name="from">Start of the time range (UTC, ISO 8601).</param>
/// <param name="to">End of the time range (UTC, ISO 8601).</param>
/// <param name="granularity">Bucket granularity: "FiveMinute", "Hour", or "Day".</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Historical usage data points within the requested range.</returns>
/// <response code="200">Returns the historical usage data.</response>
/// <response code="400">Invalid filterType or granularity.</response>
[HttpGet("historical-usage")]
[ProducesResponseType(typeof(HistoricalUsageResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetHistoricalUsage(
    [FromQuery] string filterType,
    [FromQuery] string targetId,
    [FromQuery] string? clientId,
    [FromQuery] DateTime from,
    [FromQuery] DateTime to,
    [FromQuery] string granularity,
    CancellationToken cancellationToken)
{
    if (!IsValidFilterType(filterType))
        return BadRequest("filterType must be 'Service' or 'ResourcePool'.");

    if (!Enum.TryParse<BucketGranularity>(granularity, ignoreCase: true, out var parsedGranularity))
        return BadRequest("granularity must be 'FiveMinute', 'Hour', or 'Day'.");

    var result = await _statisticsService.GetHistoricalUsageAsync(
        filterType, targetId, clientId, from, to, parsedGranularity, cancellationToken);

    return Ok(result);
}
```

### 7. Add `using` statements

Ensure the necessary `using` directives are added to files that reference the new types:
- `ClientManager.Shared.Models.Enums` (for `BucketGranularity`, `UsageEventType`)
- `ClientManager.Shared.Models.Entities` (for `UsageSnapshot`, `UsageBucket`)
- `ClientManager.DataAccess.Interfaces` (for `IUsageSnapshotRepository`)
- `ClientManager.Api.Models.Responses` (for `HistoricalUsageResponse`)

## Verification

- Solution compiles without errors
- `GET /api/statistics/historical-usage?filterType=Service&targetId=auth-service&from=2026-03-19T00:00:00Z&to=2026-03-19T23:59:59Z&granularity=FiveMinute` returns data points after traffic has been flowing for >5 minutes
- `GET /api/statistics/historical-usage?filterType=ResourcePool&targetId=db-connections&clientId=client1&from=...&to=...&granularity=Hour` returns per-client data
- `GET /api/statistics/usage-timeseries` returns real usage values instead of zeros
- `GET /api/statistics/global-usage` returns a non-zero `RequestsPerMinute` when traffic is flowing
- Swagger docs show the new endpoint with correct parameter descriptions and response types
- Invalid `filterType` or `granularity` returns 400
