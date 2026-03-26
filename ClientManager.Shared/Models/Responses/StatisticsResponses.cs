namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// High-level system overview statistics.
/// </summary>
/// <param name="TotalClients">Total number of client configurations.</param>
/// <param name="EnabledClients">Number of currently enabled clients.</param>
/// <param name="TotalServices">Total number of service definitions.</param>
/// <param name="EnabledServices">Number of currently enabled services.</param>
/// <param name="TotalResourcePools">Total number of resource pool definitions.</param>
/// <param name="ActiveAllocations">Total active resource allocations across all pools.</param>
public record SystemOverviewResponse(
    int TotalClients,
    int EnabledClients,
    int TotalServices,
    int EnabledServices,
    int TotalResourcePools,
    int ActiveAllocations);

/// <summary>
/// Summary statistics for a single client.
/// </summary>
/// <param name="ClientId">The unique identifier of the client.</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="IsEnabled">Whether the client is currently enabled.</param>
/// <param name="ServiceCount">Number of services the client has access entries for.</param>
/// <param name="ResourcePoolCount">Number of resource pools the client has quota entries for.</param>
/// <param name="HasGlobalRateLimit">Whether the client has a global rate limit configured.</param>
public record ClientSummaryResponse(
    string ClientId,
    string Name,
    bool IsEnabled,
    int ServiceCount,
    int ResourcePoolCount,
    bool HasGlobalRateLimit);

/// <summary>
/// Per-service usage statistics.
/// </summary>
/// <param name="ServiceId">The unique identifier of the service.</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="IsEnabled">Whether the service is currently enabled.</param>
/// <param name="ClientCount">Number of clients that have an access entry for this service.</param>
/// <param name="HasGlobalRateLimit">Whether a global rate limit exists for this service.</param>
public record ServiceStatisticsResponse(
    string ServiceId,
    string Name,
    bool IsEnabled,
    int ClientCount,
    bool HasGlobalRateLimit);

/// <summary>
/// Per-resource-pool utilization statistics.
/// </summary>
/// <param name="ResourcePoolId">The unique identifier of the resource pool.</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="MaxSlots">System-wide maximum concurrent allocations.</param>
/// <param name="ActiveAllocations">Current number of active allocations.</param>
/// <param name="AvailableSlots">Remaining slots available for allocation.</param>
/// <param name="HasGlobalRateLimit">Whether a global rate limit exists for this pool.</param>
public record ResourcePoolStatisticsResponse(
    string ResourcePoolId,
    string Name,
    int MaxSlots,
    int ActiveAllocations,
    int AvailableSlots,
    bool HasGlobalRateLimit);

/// <summary>
/// Global usage statistics including request rate and pool acquisition.
/// </summary>
/// <param name="RequestsPerMinute">Estimated requests per minute across the system.</param>
/// <param name="TotalPoolSlots">Total number of pool slots across all resource pools.</param>
/// <param name="AcquiredPoolSlots">Number of pool slots currently acquired.</param>
/// <param name="AcquisitionPercentage">Percentage of pool slots currently acquired.</param>
public record GlobalUsageStatsResponse(
    double RequestsPerMinute,
    int TotalPoolSlots,
    int AcquiredPoolSlots,
    double AcquisitionPercentage);

/// <summary>
/// Summary of all clients with their service and pool access statistics for the dashboard.
/// </summary>
/// <param name="Rows">List of per-client summary rows.</param>
public record ClientSummariesResponse(
    IReadOnlyList<ClientSummaryRow> Rows);

/// <summary>
/// A single row in the client summary table.
/// </summary>
/// <param name="ClientId">The unique identifier of the client.</param>
/// <param name="DisplayName">Human-readable display name.</param>
/// <param name="AccessibleServices">Number of services the client can access.</param>
/// <param name="TotalRateLimitCap">Formatted string summarizing the client's total rate limit capacity.</param>
/// <param name="AccessiblePools">Number of resource pools the client has access to.</param>
/// <param name="UsedSlots">Number of resource pool slots currently in use.</param>
/// <param name="TotalAccessibleSlots">Total number of resource pool slots the client can access.</param>
public record ClientSummaryRow(
    string ClientId,
    string DisplayName,
    int AccessibleServices,
    string TotalRateLimitCap,
    int AccessiblePools,
    int UsedSlots,
    int TotalAccessibleSlots);
