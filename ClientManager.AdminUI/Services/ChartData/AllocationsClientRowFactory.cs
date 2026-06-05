using ClientManager.AdminUI.Models.Allocations;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class AllocationsClientRowFactory
{
    internal static AllocationClientRow Create(
        AllocationsLoadContext context,
        string clientId,
        string clientName,
        ResourcePoolStatisticsResponse pool,
        ClientUsageEntry? recentEntry,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup)
    {
        var currentValue = context.IsAccessMetric
            ? recentEntry?.GrantedCount ?? 0
            : recentEntry?.ActiveCount ?? 0;
        var capValue = context.IsAccessMetric
            ? AllocationsCapCalculator.GetEffectiveClientPoolCap(
                clientId, pool.ResourcePoolId, context.AllClients, rateLimitLookup, AllocationsLoadContext.RecentWindow)
            : AllocationsCapCalculator.GetClientPoolMaxSlots(
                clientId, pool.ResourcePoolId, pool.MaxSlots, context.AllClients);

        return new AllocationClientRow(
            clientId, clientName, pool.Name, currentValue, capValue, recentEntry?.DeniedCount ?? 0);
    }
}
