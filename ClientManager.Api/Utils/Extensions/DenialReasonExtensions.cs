using ClientManager.Api.Models.Enums;

namespace ClientManager.Api.Utils.Extensions;

/// <summary>
/// Extension methods for converting denial reason enums to metric tag values.
/// </summary>
public static class DenialReasonExtensions
{
    public static string ToTagValue(this ServiceAccessDenialReason reason) => reason switch
    {
        ServiceAccessDenialReason.NotConfigured => "not_configured",
        ServiceAccessDenialReason.ClientDisabled => "client_disabled",
        ServiceAccessDenialReason.ServiceDisabled => "service_disabled",
        ServiceAccessDenialReason.NotAllowed => "not_allowed",
        ServiceAccessDenialReason.GlobalRateLimited => "global_rate_limited",
        ServiceAccessDenialReason.RateLimited => "rate_limited",
        _ => "unknown"
    };

    public static string ToTagValue(this ResourceAllocationDenialReason reason) => reason switch
    {
        ResourceAllocationDenialReason.ClientCapReached => "client_cap_reached",
        ResourceAllocationDenialReason.RateLimited => "rate_limited",
        ResourceAllocationDenialReason.NoSlots => "no_slots",
        _ => "unknown"
    };
}
