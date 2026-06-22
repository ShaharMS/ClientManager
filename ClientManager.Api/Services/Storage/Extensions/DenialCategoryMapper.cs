using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.Storage.Extensions;

internal static class DenialCategoryMapper
{
    internal static UsageDenialCategory FromServiceReason(ServiceAccessDenialReason reason) => reason switch
    {
        ServiceAccessDenialReason.NotConfigured => UsageDenialCategory.Unauthenticated,
        ServiceAccessDenialReason.NotAllowed or ServiceAccessDenialReason.ClientDisabled or ServiceAccessDenialReason.ServiceDisabled
            => UsageDenialCategory.Blocked,
        ServiceAccessDenialReason.GlobalRateLimited or ServiceAccessDenialReason.RateLimited
            => UsageDenialCategory.RateLimited,
        _ => UsageDenialCategory.Blocked
    };

    internal static UsageDenialCategory FromPoolReason(ResourceAllocationDenialReason reason) => reason switch
    {
        ResourceAllocationDenialReason.RateLimited => UsageDenialCategory.RateLimited,
        ResourceAllocationDenialReason.ClientCapReached or ResourceAllocationDenialReason.NoSlots
            => UsageDenialCategory.CapacityLimited,
        _ => UsageDenialCategory.Blocked
    };
}
