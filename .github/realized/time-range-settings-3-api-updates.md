# Plan: Time Range Filtering & Settings — Step 3: API Updates

> **Status**: ✅ Completed
> **Prerequisite**: [time-range-settings-2-settings-page.md](time-range-settings-2-settings-page.md)
> **Next**: [time-range-settings-4-selector-component.md](time-range-settings-4-selector-component.md)
> **Parent**: [time-range-settings-overview.md](time-range-settings-overview.md)

## TL;DR

Add optional `from` and `to` query parameters to the `usage-timeseries` and `client-usage-breakdown` API endpoints so the AdminUI can request data for arbitrary time ranges instead of the current hardcoded 1-hour window. Update the corresponding `IStatisticsService` methods and `StatisticsApiService` client. The `historical-usage` endpoint already supports time ranges and needs no changes.

## Reference Pattern

In [ClientManager.Api/Controllers/StatisticsController.cs](../../ClientManager.Api/Controllers/StatisticsController.cs):
- The `GetHistoricalUsage` action already accepts `[FromQuery] DateTime from`, `[FromQuery] DateTime to`, `[FromQuery] BucketGranularity granularity` — follow the same pattern.
- XML doc comments and `[ProducesResponseType]` attributes on every action.

In [ClientManager.Api/Services/StatisticsService.cs](../../ClientManager.Api/Services/StatisticsService.cs):
- `GetUsageTimeSeriesAsync` currently hardcodes a 13-slot (last-hour) window from `RoundDownToFiveMinutes(now)`.
- `GetClientUsageBreakdownAsync` uses only the latest 5-minute bucket.
- Both need to accept `from`/`to`/`granularity` and aggregate accordingly.

In [ClientManager.AdminUI/Services/StatisticsApiService.cs](../../ClientManager.AdminUI/Services/StatisticsApiService.cs):
- `GetUsageTimeSeriesAsync` and `GetClientUsageBreakdownAsync` need `from`, `to`, `granularity` parameters.

## Steps

### 1. Update `IStatisticsService` interface

In `ClientManager.Api/Interfaces/IStatisticsService.cs`, update the signatures:

```csharp
// Before:
Task<UsageTimeSeriesResponse> GetUsageTimeSeriesAsync(
    GlobalRateLimitTarget targetType, string targetId, IEnumerable<string>? clientIds,
    CancellationToken cancellationToken = default);

Task<ClientUsageBreakdownResponse> GetClientUsageBreakdownAsync(
    GlobalRateLimitTarget targetType, string targetId, IEnumerable<string>? clientIds,
    CancellationToken cancellationToken = default);

// After:
Task<UsageTimeSeriesResponse> GetUsageTimeSeriesAsync(
    GlobalRateLimitTarget targetType, string targetId, IEnumerable<string>? clientIds,
    DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
    CancellationToken cancellationToken = default);

Task<ClientUsageBreakdownResponse> GetClientUsageBreakdownAsync(
    GlobalRateLimitTarget targetType, string targetId, IEnumerable<string>? clientIds,
    DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
    CancellationToken cancellationToken = default);
```

### 2. Update `StatisticsService` implementation

In `ClientManager.Api/Services/StatisticsService.cs`:

**`GetUsageTimeSeriesAsync`** — add `from`/`to`/`granularity` params. When provided, use the specified range and granularity. When not provided, default to the current behavior (last hour, FiveMinute granularity, 13 slots):

```csharp
public async Task<UsageTimeSeriesResponse> GetUsageTimeSeriesAsync(
    GlobalRateLimitTarget targetType, string targetId, IEnumerable<string>? clientIds,
    DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
    CancellationToken cancellationToken = default)
{
    var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
    var now = DateTime.UtcNow;
    var effectiveTo = to ?? now;
    var effectiveFrom = from ?? RoundDownToFiveMinutes(now).AddMinutes(-60);

    // ... get cap value (same as before) ...

    var snapshots = await _usageSnapshotRepository.GetByTargetAsync(
        targetId, targetType, effectiveGranularity, cancellationToken);

    // Filter by client IDs if specified
    var clientIdSet = clientIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (clientIdSet is not null)
    {
        snapshots = snapshots.Where(s => clientIdSet.Contains(s.ClientId)).ToList();
    }

    // Aggregate across all clients, filtering by time range
    var aggregated = new SortedDictionary<DateTime, double>();
    foreach (var snapshot in snapshots)
    {
        foreach (var bucket in snapshot.Buckets)
        {
            if (bucket.Timestamp < effectiveFrom || bucket.Timestamp > effectiveTo)
                continue;

            if (aggregated.TryGetValue(bucket.Timestamp, out var existing))
                aggregated[bucket.Timestamp] = existing + bucket.GrantedCount;
            else
                aggregated[bucket.Timestamp] = bucket.GrantedCount;
        }
    }

    var usagePoints = aggregated
        .Select(kvp => new TimeSeriesPoint(kvp.Key, kvp.Value)).ToList();
    var capPoints = usagePoints
        .Select(p => new TimeSeriesPoint(p.Timestamp, capValue)).ToList();

    return new UsageTimeSeriesResponse(usagePoints, capPoints);
}
```

**`GetClientUsageBreakdownAsync`** — add `from`/`to`/`granularity` params. When provided, sum granted counts across all buckets in range. When not provided, default to latest 5-min bucket:

```csharp
public async Task<ClientUsageBreakdownResponse> GetClientUsageBreakdownAsync(
    GlobalRateLimitTarget targetType, string targetId, IEnumerable<string>? clientIds,
    DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
    CancellationToken cancellationToken = default)
{
    var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
    var now = DateTime.UtcNow;

    var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);
    var clientIdSet = clientIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var snapshots = await _usageSnapshotRepository.GetByTargetAsync(
        targetId, targetType, effectiveGranularity, cancellationToken);

    var entries = new List<ClientUsageEntry>();

    foreach (var client in clients)
    {
        if (clientIdSet is not null && !clientIdSet.Contains(client.Id))
            continue;

        var snapshot = snapshots.FirstOrDefault(s =>
            string.Equals(s.ClientId, client.Id, StringComparison.OrdinalIgnoreCase));
        if (snapshot is null) continue;

        double count;
        if (from is not null && to is not null)
        {
            count = snapshot.Buckets
                .Where(b => b.Timestamp >= from && b.Timestamp <= to)
                .Sum(b => b.GrantedCount);
        }
        else
        {
            var latestBucketTime = RoundDownToFiveMinutes(now).AddMinutes(-5);
            count = snapshot.Buckets
                .Where(b => b.Timestamp == latestBucketTime)
                .Sum(b => b.GrantedCount);
        }

        if (count > 0)
        {
            entries.Add(new ClientUsageEntry(client.Id, client.Name, count));
        }
    }

    return new ClientUsageBreakdownResponse(entries);
}
```

### 3. Update `StatisticsController` actions

In `ClientManager.Api/Controllers/StatisticsController.cs`, add optional query params to both endpoints:

**`GetUsageTimeSeries`**:
```csharp
[HttpGet("usage-timeseries")]
[ProducesResponseType(typeof(UsageTimeSeriesResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> GetUsageTimeSeries(
    [FromQuery] GlobalRateLimitTarget filterType,
    [FromQuery] string targetId,
    [FromQuery] string? clientIds,
    [FromQuery] DateTime? from,
    [FromQuery] DateTime? to,
    [FromQuery] BucketGranularity? granularity,
    CancellationToken cancellationToken)
{
    var clientIdList = ParseClientIds(clientIds);
    var result = await _statisticsService.GetUsageTimeSeriesAsync(
        filterType, targetId, clientIdList, from, to, granularity, cancellationToken);
    return Ok(result);
}
```

**`GetClientUsageBreakdown`**:
```csharp
[HttpGet("client-usage-breakdown")]
[ProducesResponseType(typeof(ClientUsageBreakdownResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> GetClientUsageBreakdown(
    [FromQuery] GlobalRateLimitTarget filterType,
    [FromQuery] string targetId,
    [FromQuery] string? clientIds,
    [FromQuery] DateTime? from,
    [FromQuery] DateTime? to,
    [FromQuery] BucketGranularity? granularity,
    CancellationToken cancellationToken)
{
    var clientIdList = ParseClientIds(clientIds);
    var result = await _statisticsService.GetClientUsageBreakdownAsync(
        filterType, targetId, clientIdList, from, to, granularity, cancellationToken);
    return Ok(result);
}
```

### 4. Update `StatisticsApiService` (AdminUI client)

In `ClientManager.AdminUI/Services/StatisticsApiService.cs`, update both methods to accept and pass `from`/`to`/`granularity`:

```csharp
public async Task<UsageTimeSeries?> GetUsageTimeSeriesAsync(
    string filterType, string targetId, IEnumerable<string>? clientIds,
    DateTime? from = null, DateTime? to = null, string? granularity = null)
{
    var url = $"api/statistics/usage-timeseries?filterType={Uri.EscapeDataString(filterType)}&targetId={Uri.EscapeDataString(targetId)}";
    if (clientIds?.Any() == true)
        url += $"&clientIds={Uri.EscapeDataString(string.Join(",", clientIds))}";
    if (from is not null)
        url += $"&from={from.Value:O}";
    if (to is not null)
        url += $"&to={to.Value:O}";
    if (granularity is not null)
        url += $"&granularity={Uri.EscapeDataString(granularity)}";

    return await _httpClient.GetFromJsonAsync<UsageTimeSeries>(url);
}

public async Task<ClientUsageBreakdown?> GetClientUsageBreakdownAsync(
    string filterType, string targetId, IEnumerable<string>? clientIds,
    DateTime? from = null, DateTime? to = null, string? granularity = null)
{
    var url = $"api/statistics/client-usage-breakdown?filterType={Uri.EscapeDataString(filterType)}&targetId={Uri.EscapeDataString(targetId)}";
    if (clientIds?.Any() == true)
        url += $"&clientIds={Uri.EscapeDataString(string.Join(",", clientIds))}";
    if (from is not null)
        url += $"&from={from.Value:O}";
    if (to is not null)
        url += $"&to={to.Value:O}";
    if (granularity is not null)
        url += $"&granularity={Uri.EscapeDataString(granularity)}";

    return await _httpClient.GetFromJsonAsync<ClientUsageBreakdown>(url);
}
```

### 5. Ensure backward compatibility

The new parameters are all optional with sensible defaults. Existing callers that don't pass `from`/`to`/`granularity` get the same behavior as before:
- `GetUsageTimeSeriesAsync`: defaults to last hour, FiveMinute granularity
- `GetClientUsageBreakdownAsync`: defaults to latest 5-min bucket

No changes to `GetHistoricalUsageAsync` — it already accepts time range parameters.

## Verification

- API project compiles without errors.
- AdminUI project compiles without errors.
- **Test via Swagger or HTTP file**: `GET /api/statistics/usage-timeseries?filterType=Service&targetId={id}` still returns data (backward compat).
- **Test via Swagger or HTTP file**: `GET /api/statistics/usage-timeseries?filterType=Service&targetId={id}&from=2026-03-20T00:00:00Z&to=2026-03-20T12:00:00Z&granularity=Hour` returns hourly data.
- **Test via Swagger or HTTP file**: `GET /api/statistics/client-usage-breakdown?filterType=Service&targetId={id}&from=2026-03-20T00:00:00Z&to=2026-03-20T12:00:00Z&granularity=Hour` returns summed client breakdown for the range.
- **UI: Navigate to Dashboard — charts still render with existing data (default 1h behavior preserved).**
