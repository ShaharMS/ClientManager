namespace ClientManager.Api.Models.Responses;

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
