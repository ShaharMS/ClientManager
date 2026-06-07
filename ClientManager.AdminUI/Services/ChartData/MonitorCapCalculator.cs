using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.AdminUI.Services.ChartData;

public static class MonitorCapCalculator
{
    public static int GetEffectiveClientServiceCap(
        string clientId,
        string serviceId,
        IReadOnlyList<ClientConfiguration> allClients,
        IReadOnlyDictionary<string, GlobalRateLimit> globalLimitsByService,
        TimeSpan comparisonWindow)
    {
        var client = allClients.FirstOrDefault(c => c.Id == clientId);
        if (client is null)
        {
            return GetScaledGlobalServiceCap(serviceId, globalLimitsByService, comparisonWindow);
        }

        var serviceSettings = client.Services.GetValueOrDefault(serviceId);
        var caps = new List<int>();

        if (serviceSettings?.RateLimit is not null)
        {
            caps.Add(RateLimitCapScaler.ScaleRateLimitCap(
                serviceSettings.RateLimit.MaxRequests,
                serviceSettings.RateLimit.Window,
                comparisonWindow));
        }

        if (client.GlobalRateLimit is not null)
        {
            caps.Add(RateLimitCapScaler.ScaleRateLimitCap(
                client.GlobalRateLimit.MaxRequests,
                client.GlobalRateLimit.Window,
                comparisonWindow));
        }

        var exemptFromServiceGlobal = serviceSettings?.ExemptFromGlobalLimit ?? client.ExemptFromGlobalLimits;
        if (!exemptFromServiceGlobal)
        {
            var globalCap = GetScaledGlobalServiceCap(serviceId, globalLimitsByService, comparisonWindow);
            if (globalCap > 0)
            {
                caps.Add(globalCap);
            }
        }

        return caps.Count > 0 ? caps.Min() : 0;
    }

    public static int GetScaledGlobalServiceCap(
        string serviceId,
        IReadOnlyDictionary<string, GlobalRateLimit> globalLimitsByService,
        TimeSpan comparisonWindow)
    {
        return globalLimitsByService.TryGetValue(serviceId, out var globalLimit)
            ? RateLimitCapScaler.ScaleRateLimitCap(globalLimit.MaxRequests, globalLimit.Window, comparisonWindow)
            : 0;
    }
}
