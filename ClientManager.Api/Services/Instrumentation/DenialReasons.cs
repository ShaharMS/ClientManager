namespace ClientManager.Api.Services.Instrumentation;

/// <summary>
/// Reasons an access check can be denied, used as a metric tag dimension.
/// </summary>
public enum AccessDenialReason
{
    NotConfigured,
    ClientDisabled,
    ServiceDisabled,
    NotAllowed,
    GlobalRateLimited,
    RateLimited
}

/// <summary>
/// Reasons a resource acquisition can be denied, used as a metric tag dimension.
/// </summary>
public enum ResourceDenialReason
{
    ClientCapReached,
    RateLimited,
    NoSlots
}

/// <summary>
/// Extension methods for converting denial reason enums to metric tag values.
/// </summary>
public static class DenialReasonExtensions
{
    public static string ToTagValue(this AccessDenialReason reason) => reason switch
    {
        AccessDenialReason.NotConfigured => "not_configured",
        AccessDenialReason.ClientDisabled => "client_disabled",
        AccessDenialReason.ServiceDisabled => "service_disabled",
        AccessDenialReason.NotAllowed => "not_allowed",
        AccessDenialReason.GlobalRateLimited => "global_rate_limited",
        AccessDenialReason.RateLimited => "rate_limited",
        _ => "unknown"
    };

    public static string ToTagValue(this ResourceDenialReason reason) => reason switch
    {
        ResourceDenialReason.ClientCapReached => "client_cap_reached",
        ResourceDenialReason.RateLimited => "rate_limited",
        ResourceDenialReason.NoSlots => "no_slots",
        _ => "unknown"
    };
}
