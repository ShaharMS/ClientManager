using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.Storage.Extensions;

/// <summary>
/// Converts denial reason enums to metric tag values.
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
}