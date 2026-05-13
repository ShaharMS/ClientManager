# Plan: Storage Statistics Performance — Step 3: Batch History API Contract

> **Status**: ✅ Completed
> **Prerequisite**: [storage-statistics-performance-2-storage-history-aggregation.md](storage-statistics-performance-2-storage-history-aggregation.md)
> **Next**: [storage-statistics-performance-4-admin-ui-graph-batching.md](storage-statistics-performance-4-admin-ui-graph-batching.md)
> **Parent**: [storage-statistics-performance-overview.md](storage-statistics-performance-overview.md)

## TL;DR

Add an explicit API contract for fetching historical graph data per client in one request. This lets UI chart pages render stacked per-client series without issuing one public API call per client.

## Reference Pattern

In [../../ClientManager.Shared/Models/Responses/UsageResponses.cs](../../ClientManager.Shared/Models/Responses/UsageResponses.cs):
- Keep small response records in the shared project so StorageApi, Api, and AdminUI agree on contract shape.
- Follow the existing `HistoricalUsageResponse` and `TargetClientUsageBreakdownResponse` style.

In [../../ClientManager.StorageApi/Controllers/StatisticsReadController.cs](../../ClientManager.StorageApi/Controllers/StatisticsReadController.cs):
- Keep controllers thin: parse query IDs, delegate to `IStatisticsService`, and document Swagger response types.
- Add XML summaries and `ProducesResponseType` attributes for any new endpoint.

In [../../ClientManager.Api/Controllers/StatisticsController.cs](../../ClientManager.Api/Controllers/StatisticsController.cs) and [../../ClientManager.Api/Services/InternalClients/Implementations/StatisticsReadClient.cs](../../ClientManager.Api/Services/InternalClients/Implementations/StatisticsReadClient.cs):
- Mirror storage read endpoints through the public API using `IStatisticsReadClient` and `StorageApiRoutes`.
- Keep the public API independent from AdminUI.

## Steps

### 1. Add a per-client historical response record

Edit [../../ClientManager.Shared/Models/Responses/UsageResponses.cs](../../ClientManager.Shared/Models/Responses/UsageResponses.cs) and add a response record that identifies both target and client for each point collection.

```csharp
public record ClientHistoricalUsageResponse(
    string TargetId,
    TargetType TargetType,
    string ClientId,
    BucketGranularity Granularity,
    IReadOnlyList<HistoricalUsagePoint> Points);
```

Do not add `ClientId` to `HistoricalUsageResponse`; keep the existing response stable for aggregate target history.

### 2. Add storage service support

Edit [../../ClientManager.StorageApi/Services/Interfaces/StatisticsReadServices.cs](../../ClientManager.StorageApi/Services/Interfaces/StatisticsReadServices.cs) and [../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs](../../ClientManager.StorageApi/Services/Implementations/StatisticsService.cs) to add `GetHistoricalUsageByClientAsync`.

The implementation should reuse the Step 2 batched history aggregation helper. It should return one response for each requested target/client pair that has points, and it should preserve empty responses only if an existing chart consumer needs them for layout.

### 3. Add the storage internal endpoint

Edit [../../ClientManager.StorageApi/Controllers/StatisticsReadController.cs](../../ClientManager.StorageApi/Controllers/StatisticsReadController.cs) and add a route such as `GET internal/v1/statistics/historical-usage/by-client` with query parameters:

- `filterType`
- `targetIds`
- `clientIds`
- `from`
- `to`
- `granularity`

Add XML docs for the controller action and `[ProducesResponseType(typeof(List<ClientHistoricalUsageResponse>), StatusCodes.Status200OK)]`.

### 4. Mirror the contract through the public API

Edit [../../ClientManager.Api/Services/InternalClients/Interfaces/IStatisticsReadClient.cs](../../ClientManager.Api/Services/InternalClients/Interfaces/IStatisticsReadClient.cs), [../../ClientManager.Api/Services/InternalClients/Implementations/StatisticsReadClient.cs](../../ClientManager.Api/Services/InternalClients/Implementations/StatisticsReadClient.cs), and [../../ClientManager.Api/Services/InternalClients/StorageApiRoutes.cs](../../ClientManager.Api/Services/InternalClients/StorageApiRoutes.cs) to add the internal-client method and route builder.

Edit [../../ClientManager.Api/Controllers/StatisticsController.cs](../../ClientManager.Api/Controllers/StatisticsController.cs) to expose the public `GET api/v1/statistics/historical-usage/by-client` action. Follow the existing statistics action documentation style and include `ProducesResponseType`.

## Verification

- `dotnet build ClientManager.Shared/ClientManager.Shared.csproj`
- `dotnet build ClientManager.StorageApi/ClientManager.StorageApi.csproj`
- `dotnet build ClientManager.Api/ClientManager.Api.csproj`
- Use Swagger or an HTTP request to call the new public endpoint for multiple services and multiple clients over a seven-day range; verify each response row includes the expected target ID, client ID, granularity, and points.
- UI: Navigate to `/swagger` on the public API and verify the new statistics endpoint appears with useful XML documentation.
- UI: Navigate to StorageApi `/docs` and verify the internal endpoint appears under statistics reads.
- UI: Keep `/` open while calling the new endpoint manually and verify the existing dashboard still loads through the old endpoint until Step 4 switches the UI.