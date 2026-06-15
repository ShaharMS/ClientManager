using ClientManager.Shared.Models.Enums;

namespace ClientManager.AdminUI.Utils;

internal static class RateLimitStrategyBadge
{
    public static string GetCssClass(RateLimitStrategy strategy) => strategy switch
    {
        RateLimitStrategy.FixedWindow => "cm-badge cm-badge--strategy-fixed-window",
        RateLimitStrategy.TokenBucket => "cm-badge cm-badge--strategy-token-bucket",
        RateLimitStrategy.ApproximateSlidingWindow => "cm-badge cm-badge--strategy-sliding-window",
        _ => "cm-badge cm-badge--success"
    };
}
