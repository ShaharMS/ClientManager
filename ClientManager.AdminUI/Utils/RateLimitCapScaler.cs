namespace ClientManager.AdminUI.Utils;

public static class RateLimitCapScaler
{
    public static int ScaleRateLimitCap(int maxRequests, TimeSpan configuredWindow, TimeSpan comparisonWindow)
    {
        if (maxRequests <= 0 || configuredWindow <= TimeSpan.Zero || comparisonWindow <= TimeSpan.Zero)
        {
            return 0;
        }

        var scaled = maxRequests * comparisonWindow.TotalSeconds / configuredWindow.TotalSeconds;
        return scaled > 0
            ? Math.Max(1, (int)Math.Round(scaled, MidpointRounding.AwayFromZero))
            : 0;
    }
}
