using ClientManager.Api.Utils.Extensions;

namespace ClientManager.Api.Models.Enums;

/// <summary>
/// Reasons a resource acquisition can be denied, used as a metric tag dimension.
/// Each value maps to a snake_case string via <see cref="DenialReasonExtensions.ToTagValue(ResourceAllocationDenialReason)"/>
/// for consistent metric labels.
/// </summary>
public enum ResourceAllocationDenialReason
{
    ClientCapReached,
    RateLimited,
    NoSlots
}