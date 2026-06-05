using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

public static class AllocationsCapCalculator
{
    public static int GetClientPoolMaxSlots(
        string clientId,
        string poolId,
        int poolMaxSlots,
        IReadOnlyList<ClientConfiguration> allClients)
    {
        var client = allClients.FirstOrDefault(c => c.Id == clientId);
        if (client?.ResourcePools.TryGetValue(poolId, out var settings) == true)
        {
            return (int)settings.MaxSlots;
        }

        return poolMaxSlots;
    }

    public static int GetEffectiveClientPoolCap(
        string clientId,
        string poolId,
        IReadOnlyList<ClientConfiguration> allClients,
        IReadOnlyDictionary<string, GlobalRateLimit> globalLimitsByPool,
        TimeSpan comparisonWindow)
    {
        var client = allClients.FirstOrDefault(c => c.Id == clientId);
        if (client is null)
        {
            return GetScaledGlobalPoolCap(poolId, globalLimitsByPool, comparisonWindow);
        }

        var caps = new List<int>();

        if (client.GlobalRateLimit is not null)
        {
            caps.Add(RateLimitCapScaler.ScaleRateLimitCap(
                client.GlobalRateLimit.MaxRequests,
                client.GlobalRateLimit.Window,
                comparisonWindow));
        }

        if (!client.ExemptFromGlobalLimits)
        {
            var globalCap = GetScaledGlobalPoolCap(poolId, globalLimitsByPool, comparisonWindow);
            if (globalCap > 0)
            {
                caps.Add(globalCap);
            }
        }

        return caps.Count > 0 ? caps.Min() : 0;
    }

    public static int GetScaledGlobalPoolCap(
        string poolId,
        IReadOnlyDictionary<string, GlobalRateLimit> globalLimitsByPool,
        TimeSpan comparisonWindow)
    {
        return globalLimitsByPool.TryGetValue(poolId, out var globalLimit)
            ? RateLimitCapScaler.ScaleRateLimitCap(globalLimit.MaxRequests, globalLimit.Window, comparisonWindow)
            : 0;
    }

    public static int GetPoolChartCap(
        ResourcePoolStatisticsResponse pool,
        bool isAccessMetric,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        TimeSpan comparisonWindow)
        => isAccessMetric
            ? GetScaledGlobalPoolCap(pool.ResourcePoolId, rateLimitLookup, comparisonWindow)
            : pool.MaxSlots;
}
