namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Tag keys used on runtime metrics.
/// </summary>
public enum MetricTagKey
{
    ClientId,
    ServiceId,
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
