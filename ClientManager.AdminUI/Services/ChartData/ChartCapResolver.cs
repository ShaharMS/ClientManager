using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Services;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class ChartCapResolver
{
    internal static int ResolveServiceChartCap(
        string serviceId,
        IReadOnlyList<ClientUsageEntry> entries,
        IReadOnlyList<ClientConfiguration> allClients,
        IReadOnlyDictionary<string, GlobalRateLimit> globalLimitsByService,
        TimeSpan comparisonWindow)
    {
        var (cap, _) = MonitorCapCalculator.GetServiceSummaryCap(
            serviceId, entries, allClients, globalLimitsByService, comparisonWindow);
        return cap;
    }

    internal static int ResolveAllServicesChartCap(
        IEnumerable<Service> services,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        TimeSpan comparisonWindow)
    {
        var cap = 0;
        foreach (var service in services)
        {
            if (!rateLimitLookup.ContainsKey(service.Id))
            {
                return 0;
            }

            cap += MonitorCapCalculator.GetScaledGlobalServiceCap(
                service.Id, rateLimitLookup, comparisonWindow);
        }

        return cap;
    }

    internal static int ResolvePoolAccessChartCap(
        string poolId,
        IReadOnlyList<ClientUsageEntry> entries,
        IReadOnlyList<ClientConfiguration> allClients,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        TimeSpan comparisonWindow)
    {
        var globalCap = AllocationsCapCalculator.GetScaledGlobalPoolCap(
            poolId, rateLimitLookup, comparisonWindow);
        if (globalCap > 0)
        {
            return globalCap;
        }

        if (entries.Count == 0
            || !entries.All(entry => AllocationsCapCalculator.ClientHasGlobalRateLimit(entry.ClientId, allClients)))
        {
            return 0;
        }

        return entries.Sum(entry => AllocationsCapCalculator.GetEffectiveClientPoolCap(
            entry.ClientId, poolId, allClients, rateLimitLookup, comparisonWindow));
    }

    internal static int ResolvePoolSlotCap(int maxSlots) => maxSlots > 0 ? maxSlots : 0;

    internal static int ResolveAllPoolsAccessChartCap(
        IEnumerable<ResourcePoolStatisticsResponse> pools,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        TimeSpan comparisonWindow)
    {
        var cap = 0;
        foreach (var pool in pools)
        {
            var globalCap = AllocationsCapCalculator.GetScaledGlobalPoolCap(
                pool.ResourcePoolId, rateLimitLookup, comparisonWindow);
            if (globalCap <= 0)
            {
                return 0;
            }

            cap += globalCap;
        }

        return cap;
    }

    internal static int ResolveAllPoolsSlotCap(IEnumerable<ResourcePoolStatisticsResponse> pools)
    {
        var cap = 0;
        foreach (var pool in pools)
        {
            if (pool.MaxSlots <= 0)
            {
                return 0;
            }

            cap += pool.MaxSlots;
        }

        return cap;
    }

    internal static List<ChartPoint> BuildCapSeries(
        IReadOnlyList<ChartBucketAggregator.AggregatedBucket> referenceBuckets,
        int cap)
    {
        if (cap <= 0)
        {
            return [];
        }

        return referenceBuckets
            .Select(bucket => new ChartPoint(bucket.Label, cap))
            .ToList();
    }
}
