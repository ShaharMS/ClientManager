using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

internal static class OffBudgetChartSeriesBuilder
{
    internal static void AppendSeries(
        ICollection<ClientAreaSeries> series,
        string targetId,
        IReadOnlyList<ChartPoint> offBudgetPoints,
        IStringLocalizer<SharedResources> localizer)
    {
        if (!offBudgetPoints.Any(point => point.Value > 0))
        {
            return;
        }

        series.Add(new ClientAreaSeries(
            targetId + ChartAggregator.OffBudgetSuffix,
            localizer["Columns.OffBudget"],
            offBudgetPoints.ToList(),
            Hidden: true));
    }

    internal static List<ChartPoint> SumOffBudgetPoints(
        IEnumerable<ClientUsageEntry> entries,
        string serviceId,
        IReadOnlyList<ClientConfiguration> allClients,
        IReadOnlyDictionary<string, ChartBucketAggregator.AggregationResult> clientAggregations,
        IReadOnlyList<ChartBucketAggregator.AggregatedBucket> referenceBuckets)
    {
        return referenceBuckets
            .Select(bucket => new ChartPoint(
                bucket.Label,
                entries
                    .Where(entry => !MonitorCapCalculator.ContributesToGlobalServiceLimit(
                        entry.ClientId, serviceId, allClients))
                    .Where(entry => clientAggregations.ContainsKey(entry.ClientId))
                    .Sum(entry => clientAggregations[entry.ClientId].Buckets
                        .FirstOrDefault(b => b.Label == bucket.Label)?.Value ?? 0)))
            .ToList();
    }

    internal static List<ChartPoint> SumOffBudgetPoolPoints(
        IEnumerable<ClientUsageEntry> entries,
        IReadOnlyList<ClientConfiguration> allClients,
        IReadOnlyDictionary<string, ChartBucketAggregator.AggregationResult> clientAggregations,
        IReadOnlyList<ChartBucketAggregator.AggregatedBucket> referenceBuckets)
    {
        return referenceBuckets
            .Select(bucket => new ChartPoint(
                bucket.Label,
                entries
                    .Where(entry => !MonitorCapCalculator.ContributesToGlobalPoolLimit(
                        entry.ClientId, allClients))
                    .Where(entry => clientAggregations.ContainsKey(entry.ClientId))
                    .Sum(entry => clientAggregations[entry.ClientId].Buckets
                        .FirstOrDefault(b => b.Label == bucket.Label)?.Value ?? 0)))
            .ToList();
    }

    internal static (List<ChartBucketAggregator.RawPoint> Contributing, List<ChartBucketAggregator.RawPoint> OffBudget)
        PartitionClientHistoriesForPools(
            IEnumerable<ResourcePoolStatisticsResponse> pools,
            IReadOnlyList<TargetClientUsageBreakdownResponse> breakdowns,
            IReadOnlyDictionary<string, Dictionary<string, ClientHistoricalUsageResponse>> historiesByPool,
            IReadOnlyList<ClientConfiguration> allClients)
    {
        var contributing = new List<ChartBucketAggregator.RawPoint>();
        var offBudget = new List<ChartBucketAggregator.RawPoint>();

        foreach (var pool in pools)
        {
            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == pool.ResourcePoolId);
            historiesByPool.TryGetValue(pool.ResourcePoolId, out var byClient);
            byClient ??= [];

            foreach (var entry in breakdown?.Entries ?? [])
            {
                if (!byClient.TryGetValue(entry.ClientId, out var history))
                {
                    continue;
                }

                var contributes = MonitorCapCalculator.ContributesToGlobalPoolLimit(
                    entry.ClientId, allClients);

                foreach (var point in history.Points)
                {
                    var raw = new ChartBucketAggregator.RawPoint(point.Timestamp, point.GrantedCount);
                    if (contributes)
                    {
                        contributing.Add(raw);
                    }
                    else
                    {
                        offBudget.Add(raw);
                    }
                }
            }
        }

        return (contributing, offBudget);
    }

    internal static (List<ChartBucketAggregator.RawPoint> Contributing, List<ChartBucketAggregator.RawPoint> OffBudget)
        PartitionClientHistories(
            IEnumerable<Service> services,
            IReadOnlyList<TargetClientUsageBreakdownResponse> breakdowns,
            IReadOnlyDictionary<string, Dictionary<string, ClientHistoricalUsageResponse>> historiesByService,
            IReadOnlyList<ClientConfiguration> allClients)
    {
        var contributing = new List<ChartBucketAggregator.RawPoint>();
        var offBudget = new List<ChartBucketAggregator.RawPoint>();

        foreach (var service in services)
        {
            var breakdown = breakdowns.FirstOrDefault(b => b.TargetId == service.Id);
            historiesByService.TryGetValue(service.Id, out var byClient);
            byClient ??= [];

            foreach (var entry in breakdown?.Entries ?? [])
            {
                if (!byClient.TryGetValue(entry.ClientId, out var history))
                {
                    continue;
                }

                var contributes = MonitorCapCalculator.ContributesToGlobalServiceLimit(
                    entry.ClientId, service.Id, allClients);

                foreach (var point in history.Points)
                {
                    var raw = new ChartBucketAggregator.RawPoint(point.Timestamp, point.GrantedCount);
                    if (contributes)
                    {
                        contributing.Add(raw);
                    }
                    else
                    {
                        offBudget.Add(raw);
                    }
                }
            }
        }

        return (contributing, offBudget);
    }
}
