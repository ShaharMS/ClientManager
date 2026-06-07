using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Services;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed class DashboardDonutDataLoader
{
    private readonly StatisticsApiService _statsService;

    public DashboardDonutDataLoader(StatisticsApiService statsService) => _statsService = statsService;

    public async Task<List<ClientUsagePoint>> LoadAllTargetsAsync(DashboardChartLoadContext context)
    {
        var donutTo = context.TimeRange.GetTo();
        var donutFrom = context.TimeRange.GetFrom(donutTo);
        var isRateBased = context.SelectedFilterType == "Service";

        var targets = context.SelectedFilterType == "Service"
            ? context.AllServices.Where(t => t.Id != DashboardChartLoadContext.AllTargetsId)
            : context.AllPools.Where(t => t.Id != DashboardChartLoadContext.AllTargetsId);
        var targetIds = targets.Select(t => t.Id).ToList();

        var donutClients = context.SelectedClientIds?.Any() == true
            ? context.Clients.Where(c => context.SelectedClientIds.Contains(c.Id)).ToList()
            : context.Clients;

        var donutBreakdowns = await _statsService.GetClientUsageBreakdownAsync(
            context.SelectedFilterType,
            targetIds,
            context.SelectedClientIds,
            donutFrom,
            donutTo,
            context.TimeRange.Granularity);

        var totalsByClientId = donutBreakdowns
            .SelectMany(breakdown => breakdown.Entries)
            .GroupBy(entry => entry.ClientId)
            .ToDictionary(
                group => group.Key,
                group => isRateBased
                    ? group.Sum(entry => (double)entry.GrantedCount)
                    : group.Sum(entry => (double)entry.ActiveCount));

        var newDonut = new List<ClientUsagePoint>();
        foreach (var client in donutClients)
        {
            var totalValue = totalsByClientId.GetValueOrDefault(client.Id);
            if (totalValue > 0)
            {
                newDonut.Add(new ClientUsagePoint(client.Id, client.Name, totalValue));
            }
        }

        return newDonut;
    }
}
