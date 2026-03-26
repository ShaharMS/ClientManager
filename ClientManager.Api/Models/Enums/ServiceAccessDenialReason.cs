using ClientManager.Api.Utils.Extensions;

namespace ClientManager.Api.Models.Enums;


/// <summary>
/// Reasons an access check can be denied, used as a metric tag dimension.
/// Each value maps to a snake_case string via <see cref="DenialReasonExtensions.ToTagValue(ServiceAccessDenialReason)"/>
/// for consistent metric labels.
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
