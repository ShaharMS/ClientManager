# Plan: AdminUI Redesign — Step 4: New Statistics API Endpoints

> **Status**: 🔲 Not started
> **Prerequisite**: [adminui-redesign-3-dashboard.md](adminui-redesign-3-dashboard.md)
> **Next**: [adminui-redesign-5-list-pages.md](adminui-redesign-5-list-pages.md)
> **Parent**: [adminui-redesign-overview.md](adminui-redesign-overview.md)

## TL;DR

Add new API endpoints to serve the dashboard's chart and table data: usage-over-time series, per-client usage breakdowns, global usage stats, and a client summary projection. These endpoints power the line chart, donut chart, stat cards, and summary table on the redesigned Dashboard.

## Reference Pattern

In [ClientManager.Api/Controllers/StatisticsController.cs](../../ClientManager.Api/Controllers/StatisticsController.cs):
- Existing controller with `GetOverview` and `GetResourcePoolStats` endpoints
- Uses service injection and follows existing XML doc + `[ProducesResponseType]` pattern

In [ClientManager.Api/Services/StatisticsCollectionService.cs](../../ClientManager.Api/Services/StatisticsCollectionService.cs):
- Existing statistics service that reads from data stores
- Follow its pattern for the new service methods

In [ClientManager.AdminUI/Services/StatisticsApiService.cs](../../ClientManager.AdminUI/Services/StatisticsApiService.cs):
- Existing API client service with `GetOverviewAsync()` and `GetResourcePoolStatsAsync()`
- Follow its HttpClient pattern for new methods

## Steps

### 1. Define new response models

Create `ClientManager.Api/Models/Responses/UsageTimeSeriesResponse.cs`:

```csharp
namespace ClientManager.Api.Models.Responses;

public record UsageTimeSeriesResponse(
    List<TimeSeriesPoint> UsagePoints,
    List<TimeSeriesPoint> CapPoints
);

public record TimeSeriesPoint(
    DateTime Timestamp,
    double Value
);
```

Create `ClientManager.Api/Models/Responses/ClientUsageBreakdownResponse.cs`:

```csharp
namespace ClientManager.Api.Models.Responses;

public record ClientUsageBreakdownResponse(
    List<ClientUsageEntry> Entries
);

public record ClientUsageEntry(
    string ClientId,
    string ClientName,
    double Value
);
```

Create `ClientManager.Api/Models/Responses/ClientSummaryResponse.cs`:

```csharp
namespace ClientManager.Api.Models.Responses;

public record ClientSummaryResponse(
    List<ClientSummaryRow> Rows
);

public record ClientSummaryRow(
    string ClientId,
    string DisplayName,
    int AccessibleServices,
    string TotalRateLimitCap,
    int AccessiblePools,
    int UsedSlots,
    int TotalAccessibleSlots
);
```

Create `ClientManager.Api/Models/Responses/GlobalUsageStatsResponse.cs`:

```csharp
namespace ClientManager.Api.Models.Responses;

public record GlobalUsageStatsResponse(
    double RequestsPerMinute,
    int TotalPoolSlots,
    int AcquiredPoolSlots,
    double AcquisitionPercentage
);
```

### 2. Add new service interface methods

Extend or create an interface for the new statistics methods. Add to an appropriate interface (or create `IStatisticsService` if it doesn't already exist):

```csharp
Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync();
Task<UsageTimeSeriesResponse> GetUsageTimeSeriesAsync(string filterType, string targetId, IEnumerable<string>? clientIds);
Task<ClientUsageBreakdownResponse> GetClientUsageBreakdownAsync(string filterType, string targetId, IEnumerable<string>? clientIds);
Task<ClientSummaryResponse> GetClientSummariesAsync();
```

### 3. Implement the new service methods

In the existing statistics service (or a new service class if it gets too large), implement:

- **`GetGlobalUsageStatsAsync`**: Aggregate request tracking data + pool allocation stats. For pool acquisition: sum active allocations across all pools vs sum of max slots.
- **`GetUsageTimeSeriesAsync`**: Query request tracking or allocation history for the given filter (service ID or pool ID), optionally filtered by client IDs. Return time-bucketed data points + the corresponding rate limit cap as a constant series.
- **`GetClientUsageBreakdownAsync`**: For the given filter target, group usage by client. If service filter: group request counts by client. If pool filter: group slot acquisitions by client.
- **`GetClientSummariesAsync`**: For each client, project: number of allowed services, sum of rate limit caps (as a formatted string like "500 req/min"), number of accessible pools, and slots used vs total accessible slots.

**Note**: The request tracking data may require the `RequestTrackingMiddleware` to store metrics that can be queried. If the current system doesn't persist historical request data, this service should return reasonable defaults or empty data. A follow-up plan can add proper metrics storage.

### 4. Add new endpoints to `StatisticsController`

Add 4 new endpoints following the existing pattern with full XML docs and `[ProducesResponseType]` attributes:

```csharp
/// <summary>
/// Retrieves global usage statistics including request rate and pool acquisition.
/// </summary>
[HttpGet("global-usage")]
[ProducesResponseType(typeof(GlobalUsageStatsResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> GetGlobalUsageStats() { ... }

/// <summary>
/// Retrieves usage over time for a specific service or resource pool.
/// </summary>
/// <param name="filterType">Either "Service" or "ResourcePool".</param>
/// <param name="targetId">The ID of the service or resource pool.</param>
/// <param name="clientIds">Optional comma-separated client IDs to filter by.</param>
[HttpGet("usage-timeseries")]
[ProducesResponseType(typeof(UsageTimeSeriesResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetUsageTimeSeries(
    [FromQuery] string filterType, [FromQuery] string targetId,
    [FromQuery] string? clientIds) { ... }

/// <summary>
/// Retrieves per-client usage breakdown for a specific service or resource pool.
/// </summary>
[HttpGet("client-usage-breakdown")]
[ProducesResponseType(typeof(ClientUsageBreakdownResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetClientUsageBreakdown(
    [FromQuery] string filterType, [FromQuery] string targetId,
    [FromQuery] string? clientIds) { ... }

/// <summary>
/// Retrieves a summary of all clients with their service/pool access stats.
/// </summary>
[HttpGet("client-summaries")]
[ProducesResponseType(typeof(ClientSummaryResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> GetClientSummaries() { ... }
```

### 5. Add new methods to `StatisticsApiService` (AdminUI)

In `ClientManager.AdminUI/Services/StatisticsApiService.cs`, add methods to call the new endpoints:

```csharp
public async Task<GlobalUsageStatsResponse?> GetGlobalUsageStatsAsync() { ... }

public async Task<UsageTimeSeriesResponse?> GetUsageTimeSeriesAsync(
    string filterType, string targetId, IEnumerable<string>? clientIds) { ... }

public async Task<ClientUsageBreakdownResponse?> GetClientUsageBreakdownAsync(
    string filterType, string targetId, IEnumerable<string>? clientIds) { ... }

public async Task<ClientSummaryResponse?> GetClientSummariesAsync() { ... }
```

### 6. Wire Dashboard data loading to new endpoints

Update `Dashboard.razor`'s `OnInitializedAsync` and `OnFilterChanged` to call the new `StatisticsApiService` methods and populate chart/table data.

## Verification

- API project compiles without errors
- AdminUI project compiles without errors

### Required: Browser Verification

Before marking this step complete, the implementer **must**:
1. Start the API project in a background terminal and confirm it is listening.
2. Open the Swagger UI in the shared browser (using `open_browser_page` at the API's `/swagger` URL).
3. Take a screenshot showing all 4 new endpoints visible in Swagger:
   - `GET /api/statistics/global-usage`
   - `GET /api/statistics/usage-timeseries`
   - `GET /api/statistics/client-usage-breakdown`
   - `GET /api/statistics/client-summaries`
4. Execute each endpoint via Swagger's "Try it out" and verify they return 200 with valid JSON.
5. Start the AdminUI project in a background terminal and confirm it is listening.
6. Open the AdminUI Dashboard in the shared browser and take a screenshot verifying:
   - Dashboard loads data from the new endpoints
   - Charts render with real data (or empty state if no data exists)
   - Stat cards show values from the global-usage endpoint
7. Share screenshots with the user for sign-off before proceeding to the next step.
