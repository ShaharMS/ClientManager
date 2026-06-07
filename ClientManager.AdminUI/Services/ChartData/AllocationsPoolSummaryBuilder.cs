using ClientManager.AdminUI.Models.Allocations;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class AllocationsPoolSummaryBuilder
{
    internal static List<PoolSummaryRow> Build(
        IEnumerable<ResourcePoolStatisticsResponse> pools,
        IReadOnlyList<TargetClientUsageBreakdownResponse> recentBreakdowns,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        bool isAccessMetric)
    {
        if (!isAccessMetric)
        {
            return pools
                .Select(pool => new PoolSummaryRow(
                    pool.ResourcePoolId, pool.Name, pool.ActiveAllocations,
                    pool.MaxSlots, pool.AvailableSlots, 0))
                .ToList();
        }

        var rows = new List<PoolSummaryRow>();
        foreach (var pool in pools)
        {
            var recentEntries = recentBreakdowns
                .FirstOrDefault(breakdown => breakdown.TargetId == pool.ResourcePoolId)?.Entries ?? [];
            var currentValue = recentEntries.Sum(entry => entry.GrantedCount);
            var capValue = AllocationsCapCalculator.GetScaledGlobalPoolCap(
                pool.ResourcePoolId, rateLimitLookup, AllocationsLoadContext.RecentWindow);
            long? remainingValue = capValue > 0
                ? Math.Max((long)capValue - currentValue, 0)
                : null;
            var deniedCount = recentEntries.Sum(entry => entry.DeniedCount);

            rows.Add(new PoolSummaryRow(
                pool.ResourcePoolId, pool.Name, currentValue, capValue, remainingValue, deniedCount));
        }

        return rows;
    }
}
