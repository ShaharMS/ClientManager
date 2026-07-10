namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// High-level system overview statistics including live usage gauges.
/// </summary>
/// <param name="TotalClients">Total number of client configurations.</param>
/// <param name="EnabledClients">Number of currently enabled clients.</param>
/// <param name="TotalServices">Total number of service definitions.</param>
/// <param name="EnabledServices">Number of currently enabled services.</param>
/// <param name="TotalResourcePools">Total number of resource pool definitions.</param>
/// <param name="ActiveAllocations">Total active resource allocations across all pools.</param>
/// <param name="RequestsPerMinute">Estimated requests per minute across all services.</param>
/// <param name="TotalPoolSlots">Total number of pool slots across all resource pools.</param>
/// <param name="AcquiredPoolSlots">Number of pool slots currently acquired.</param>
/// <param name="AcquisitionPercentage">Percentage of pool slots currently acquired.</param>
public record SystemOverviewResponse(
    int TotalClients,
    int EnabledClients,
    int TotalServices,
    int EnabledServices,
    int TotalResourcePools,
    int ActiveAllocations,
    double RequestsPerMinute,
    int TotalPoolSlots,
    int AcquiredPoolSlots,
    double AcquisitionPercentage);

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
