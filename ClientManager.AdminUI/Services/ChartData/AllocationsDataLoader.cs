using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services.ChartData;

public sealed class AllocationsDataLoader
{
    private readonly StatisticsApiService _statsService;
    private readonly GlobalRateLimitApiService _rateLimitApi;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AllocationsDataLoader(
        StatisticsApiService statsService,
        GlobalRateLimitApiService rateLimitApi,
        IStringLocalizer<SharedResources> localizer)
    {
        _statsService = statsService;
        _rateLimitApi = rateLimitApi;
        _localizer = localizer;
    }

    public async Task<AllocationsLoadResult> LoadAsync(AllocationsLoadContext context)
    {
        var pools = context.KnownPools ?? [];
        var now = context.TimeRange.GetTo();
        var from = context.TimeRange.GetFrom(now);
        var visiblePools = context.SelectedPoolId == AllocationsLoadContext.AllPoolsId
            ? pools
            : pools.Where(pool => pool.ResourcePoolId == context.SelectedPoolId).ToList();
        var visiblePoolIds = visiblePools.Select(pool => pool.ResourcePoolId).ToList();

        var chartResponse = visiblePoolIds.Count == 0
            ? null
            : await TimeseriesChartBuilder.FetchAllocationsAsync(
                _statsService,
                visiblePoolIds,
                context.SelectedClientIds,
                context.AllClients,
                from,
                now,
                context.BucketCount,
                context.IsAccessMetric);

        var recentFrom = now.Subtract(AllocationsLoadContext.RecentWindow);
        var recentResponse = pools.Count == 0
            ? null
            : await TimeseriesChartBuilder.FetchAllocationsAsync(
                _statsService,
                pools.Select(pool => pool.ResourcePoolId),
                context.SelectedClientIds,
                context.AllClients,
                recentFrom,
                now,
                5,
                context.IsAccessMetric);

        var rateLimitLookup = context.IsAccessMetric
            ? (await _rateLimitApi.GetByTargetTypeAsync(TargetType.ResourcePool)).ToDictionary(limit => limit.TargetId)
            : new Dictionary<string, GlobalRateLimit>(StringComparer.Ordinal);

        List<TargetChartData> charts = chartResponse?.Targets is { Count: > 0 } targets
            ? [TimeseriesChartBuilder.BuildAggregateChart(
                targets,
                isRateBased: context.IsAccessMetric,
                context.AllClients,
                _localizer).Chart]
            : [];

        var clientRows = BuildClientRows(recentResponse, visiblePools, context.IsAccessMetric, rateLimitLookup, context.AllClients);
        var poolRows = BuildPoolRows(pools, recentResponse, rateLimitLookup, context.IsAccessMetric);

        return new AllocationsLoadResult(charts, clientRows, pools.ToList(), poolRows);
    }

    public bool TryRebuildFromCache(AllocationsLoadContext context, out AllocationsLoadResult result)
    {
        result = new AllocationsLoadResult([], [], [], []);
        return false;
    }

    private static List<AllocationClientRow> BuildClientRows(
        TimeseriesSearchResponse? response,
        IReadOnlyList<ResourcePoolStatisticsResponse> visiblePools,
        bool isAccessMetric,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        IReadOnlyList<ClientConfiguration> allClients)
    {
        if (response is null)
        {
            return [];
        }

        var poolNames = visiblePools.ToDictionary(pool => pool.ResourcePoolId, pool => pool.Name, StringComparer.Ordinal);
        var rows = new List<AllocationClientRow>();

        foreach (var target in response.Targets)
        {
            foreach (var clientSeries in target.ClientSeries)
            {
                var primary = isAccessMetric
                    ? clientSeries.Buckets.Sum(bucket => bucket.GrantedCount)
                    : (long)clientSeries.Buckets.LastOrDefault()?.ActiveCount;
                var denied = clientSeries.Buckets.Sum(bucket =>
                    bucket.DeniedUnauthenticatedCount
                    + bucket.DeniedBlockedCount
                    + bucket.DeniedRateLimitedCount
                    + bucket.DeniedCapacityLimitedCount);

                if (primary <= 0 && denied <= 0)
                {
                    continue;
                }

                rows.Add(new AllocationClientRow(
                    clientSeries.ClientId,
                    clientSeries.ClientName,
                    target.TargetId,
                    poolNames.GetValueOrDefault(target.TargetId, target.TargetId),
                    primary,
                    isAccessMetric
                        ? AllocationsCapCalculator.GetScaledGlobalPoolCap(target.TargetId, rateLimitLookup, AllocationsLoadContext.RecentWindow)
                        : visiblePools.FirstOrDefault(pool => pool.ResourcePoolId == target.TargetId)?.MaxSlots ?? 0,
                    denied,
                    clientSeries.Buckets.Sum(bucket => bucket.DeniedUnauthenticatedCount),
                    clientSeries.Buckets.Sum(bucket => bucket.DeniedBlockedCount),
                    clientSeries.Buckets.Sum(bucket => bucket.DeniedRateLimitedCount),
                    clientSeries.Buckets.Sum(bucket => bucket.DeniedCapacityLimitedCount)));
            }
        }

        return rows;
    }

    private static List<PoolSummaryRow> BuildPoolRows(
        IReadOnlyList<ResourcePoolStatisticsResponse> pools,
        TimeseriesSearchResponse? recentResponse,
        IReadOnlyDictionary<string, GlobalRateLimit> rateLimitLookup,
        bool isAccessMetric)
    {
        var recentByPool = recentResponse?.Targets.ToDictionary(target => target.TargetId, StringComparer.Ordinal)
            ?? new Dictionary<string, TimeseriesTargetSeries>(StringComparer.Ordinal);

        return pools.Select(pool =>
        {
            recentByPool.TryGetValue(pool.ResourcePoolId, out var target);
            var clientSeries = target?.ClientSeries ?? [];

            long currentValue;
            int capValue;
            long? remainingValue;
            long denied = 0;
            long deniedUnauth = 0;
            long deniedBlocked = 0;
            long deniedRateLimited = 0;
            long deniedCapacity = 0;

            foreach (var series in clientSeries)
            {
                denied += series.Buckets.Sum(bucket =>
                    bucket.DeniedUnauthenticatedCount + bucket.DeniedBlockedCount + bucket.DeniedRateLimitedCount + bucket.DeniedCapacityLimitedCount);
                deniedUnauth += series.Buckets.Sum(bucket => bucket.DeniedUnauthenticatedCount);
                deniedBlocked += series.Buckets.Sum(bucket => bucket.DeniedBlockedCount);
                deniedRateLimited += series.Buckets.Sum(bucket => bucket.DeniedRateLimitedCount);
                deniedCapacity += series.Buckets.Sum(bucket => bucket.DeniedCapacityLimitedCount);
            }

            if (isAccessMetric)
            {
                currentValue = clientSeries.Sum(series => series.Buckets.Sum(bucket => bucket.GrantedCount));
                capValue = AllocationsCapCalculator.GetScaledGlobalPoolCap(pool.ResourcePoolId, rateLimitLookup, AllocationsLoadContext.RecentWindow);
                remainingValue = capValue > 0 ? Math.Max(capValue - currentValue, 0L) : null;
            }
            else
            {
                currentValue = pool.ActiveAllocations;
                capValue = pool.MaxSlots;
                remainingValue = pool.AvailableSlots;
            }

            return new PoolSummaryRow(
                pool.ResourcePoolId,
                pool.Name,
                currentValue,
                capValue,
                remainingValue,
                denied,
                deniedUnauth,
                deniedBlocked,
                deniedRateLimited,
                deniedCapacity);
        }).ToList();
    }
}
