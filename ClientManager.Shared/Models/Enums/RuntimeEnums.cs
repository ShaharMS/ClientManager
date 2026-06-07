namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Tag keys used on runtime metrics.
/// </summary>
public enum MetricTagKey
{
    ClientId,
    ServiceId,
    ResourcePoolId,
    AllocationId,
    Reason
}

/// <summary>
/// Reasons an access check can be denied.
/// </summary>
public enum ServiceAccessDenialReason
{
    NotConfigured,
    ClientDisabled,
    ServiceDisabled,
    NotAllowed,
    GlobalRateLimited,
    RateLimited
}

/// <summary>
/// Reasons a resource acquisition can be denied.
/// </summary>
public enum ResourceAllocationDenialReason
{
    ClientCapReached,
    RateLimited,
    NoSlots
}
