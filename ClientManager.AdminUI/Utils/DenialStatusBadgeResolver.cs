using ClientManager.AdminUI.Localization;
using ClientManager.AdminUI.Models;

namespace ClientManager.AdminUI.Utils;

public readonly record struct DenialStatusBadge(string TermKey, StatusBadgeVariant Variant);

public static class DenialStatusBadgeResolver
{
    public static DenialStatusBadge Resolve(
        long deniedRateLimited,
        long deniedBlocked,
        long deniedUnauthenticated,
        long granted,
        int cap,
        bool includeCapacityDenials = false,
        long deniedCapacity = 0)
    {
        if (includeCapacityDenials && deniedCapacity > 0)
        {
            return new DenialStatusBadge(TermKeys.DeniedOutOfSlots, StatusBadgeVariant.Danger);
        }

        if (deniedRateLimited > 0)
        {
            var variant = cap > 0 && granted >= cap * 0.9
                ? StatusBadgeVariant.Danger
                : StatusBadgeVariant.Warning;
            return new DenialStatusBadge(TermKeys.DeniedThrottled, variant);
        }

        if (deniedBlocked > 0)
        {
            return new DenialStatusBadge(TermKeys.DeniedBlocked, StatusBadgeVariant.Warning);
        }

        if (deniedUnauthenticated > 0)
        {
            return new DenialStatusBadge(TermKeys.DeniedUnauthenticated, StatusBadgeVariant.Warning);
        }

        if (cap > 0 && granted >= cap * 0.9)
        {
            return new DenialStatusBadge(TermKeys.StateNearLimit, StatusBadgeVariant.Warning);
        }

        return new DenialStatusBadge(TermKeys.StateAvailable, StatusBadgeVariant.Success);
    }
}
