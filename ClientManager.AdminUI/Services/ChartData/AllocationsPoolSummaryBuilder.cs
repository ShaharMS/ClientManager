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
        var rows = new List<PoolSummaryRow>();
        foreach (var pool in pools)
        {
            var recentEntries = recentBreakdowns
                .FirstOrDefault(breakdown => breakdown.TargetId == pool.ResourcePoolId)?.Entries ?? [];

            long currentValue;
            int capValue;
            long? remainingValue;

            if (isAccessMetric)
            {
                currentValue = recentEntries.Sum(entry => entry.GrantedCount);
                capValue = AllocationsCapCalculator.GetScaledGlobalPoolCap(
                    pool.ResourcePoolId, rateLimitLookup, AllocationsLoadContext.RecentWindow);
                remainingValue = capValue > 0
                    ? Math.Max((long)capValue - currentValue, 0)
                    : null;
            }
            else
            {
                currentValue = pool.ActiveAllocations;
                capValue = pool.MaxSlots;
                remainingValue = pool.AvailableSlots;
            }

            rows.Add(new PoolSummaryRow(
                pool.ResourcePoolId,
                pool.Name,
                currentValue,
                capValue,
                remainingValue,
                recentEntries.Sum(entry => entry.DeniedCount),
                recentEntries.Sum(entry => entry.DeniedUnauthenticatedCount),
                recentEntries.Sum(entry => entry.DeniedBlockedCount),
                recentEntries.Sum(entry => entry.DeniedRateLimitedCount),
                recentEntries.Sum(entry => entry.DeniedCapacityLimitedCount)));
        }

        return rows;
    }
}
