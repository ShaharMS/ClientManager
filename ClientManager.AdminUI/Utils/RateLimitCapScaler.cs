using ClientManager.Shared.Models.Enums;

namespace ClientManager.AdminUI.Utils;

public static class RateLimitCapScaler
{
    public static int ScaleRateLimitCap(
        int maxRequests,
        TimeSpan configuredWindow,
        TimeSpan comparisonWindow,
        RateLimitStrategy strategy = RateLimitStrategy.FixedWindow)
    {
        if (maxRequests <= 0)
        {
            return 0;
        }

        // ponytail: token bucket cap is burst capacity, not refill rate × time
        if (strategy == RateLimitStrategy.TokenBucket)
        {
            return maxRequests;
        }

        if (configuredWindow <= TimeSpan.Zero || comparisonWindow <= TimeSpan.Zero)
        {
            return 0;
        }

        var scaled = maxRequests * comparisonWindow.TotalSeconds / configuredWindow.TotalSeconds;
        return scaled > 0
            ? Math.Max(1, (int)Math.Round(scaled, MidpointRounding.AwayFromZero))
            : 0;
    }
}
