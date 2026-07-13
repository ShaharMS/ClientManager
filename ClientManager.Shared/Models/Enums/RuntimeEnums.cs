namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Tag keys used on runtime metrics.
/// </summary>
public enum MetricTagKey
{
    /// <summary>Client identifier label on request and storage metrics.</summary>
    ClientId,

    /// <summary>Service identifier label on request and storage metrics.</summary>
    ServiceId,

    /// <summary>Access-check denial reason label when a request is rejected.</summary>
    Reason
}

/// <summary>
/// Reasons an access check can be denied.
/// </summary>
public enum ServiceAccessDenialReason
{
    /// <summary>No service access entry exists for the client-service pair.</summary>
    NotConfigured,

    /// <summary>The client configuration is disabled.</summary>
    ClientDisabled,

    /// <summary>The service definition is disabled.</summary>
    ServiceDisabled,

    /// <summary>The client-service relationship exists but is explicitly blocked.</summary>
    NotAllowed,

    /// <summary>A global per-service rate limit denied the request.</summary>
    GlobalRateLimited,

    /// <summary>A per-client rate limit denied the request.</summary>
    RateLimited
}
