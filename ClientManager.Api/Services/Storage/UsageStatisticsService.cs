using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.Api.Services.Interfaces;
namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Builds dashboard and export read models inside the storage-owning host.
/// </summary>
public partial class UsageStatisticsService : IUsageStatisticsService
{
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IGlobalRateLimitDatabase _globalRateLimitDatabase;
    private readonly IUsageSnapshotDatabase _usageSnapshotDatabase;
    private readonly IStorageReadCache _cache;

    // Constructor is defined in UsageStatisticsService.Counters.cs

    public async Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(
        CancellationToken cancellationToken = default)
        => await _cache.GetOrCreateStatisticsAsync(
            "global-usage",
            _ => ComputeGlobalUsageStatsAsync(cancellationToken),
            cancellationToken);

    public async Task<IReadOnlyList<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        TargetType targetType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds,
        DateTime? from = null,
        DateTime? to = null,
        BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default)
    {
        var targetList = NormalizeIds(targetIds);
        var clientList = NormalizeOptionalIds(clientIds);

        return await _cache.GetOrCreateStatisticsAsync(
            $"usage-timeseries:{targetType}:{CreateIdsCacheKey(targetList)}:{CreateOptionalIdsCacheKey(clientList)}:{from:O}:{to:O}:{granularity}",
            async token =>
            {
                var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
                var now = DateTime.UtcNow;
                var effectiveTo = to ?? now;
                var effectiveFrom = from ?? RoundDownToFiveMinutes(now).AddHours(-1);
                var capValues = await LoadTargetCapValuesAsync(targetType, targetList, token);
                var totalsByTarget = clientList is null
                    ? await GetContinuousBucketTotalsByTargetAsync(
                        targetList,
                        targetType,
                        null,
                        effectiveFrom,
                        effectiveTo,
                        effectiveGranularity,
                        token)
                    : null;
                var totalsByClient = clientList is null
                    ? null
                    : await GetContinuousBucketTotalsByTargetClientAsync(
                        targetList,
                        targetType,
                        clientList,
                        effectiveFrom,
                        effectiveTo,
                        effectiveGranularity,
                        token);
                var results = new List<TargetUsageTimeSeriesResponse>();

                foreach (var targetId in targetList)
                {
                    var usagePoints = GetUsageTimeSeriesPoints(targetId, clientList, totalsByTarget, totalsByClient);
                    var capValue = capValues.GetValueOrDefault(targetId);
                    var capPoints = usagePoints.Select(point => new TimeSeriesPoint(point.Timestamp, capValue)).ToList();

                    results.Add(new TargetUsageTimeSeriesResponse(targetId, usagePoints, capPoints));
                }

                return (IReadOnlyList<TargetUsageTimeSeriesResponse>)results;
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        TargetType targetType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds,
        DateTime? from = null,
        DateTime? to = null,
        BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default)
    {
        var targetList = NormalizeIds(targetIds);
        var clientList = NormalizeOptionalIds(clientIds);

        return await _cache.GetOrCreateStatisticsAsync(
            $"client-usage-breakdown:{targetType}:{CreateIdsCacheKey(targetList)}:{CreateOptionalIdsCacheKey(clientList)}:{from:O}:{to:O}:{granularity}",
            async token =>
            {
                var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
                var now = DateTime.UtcNow;
                var effectiveFrom = from ?? RoundDownToFiveMinutes(now).AddMinutes(-5);
                var effectiveTo = to ?? now;
                var clients = await _clientConfigDatabase.GetAllAsync(token);
                var clientMap = clients.ToDictionary(client => client.Id, StringComparer.Ordinal);
                var selectedClientIds = clientList ?? NormalizeIds(clients.Select(client => client.Id));
                var knownClientIds = selectedClientIds.Where(clientMap.ContainsKey).ToList();
                var totalsByClient = await GetContinuousBucketTotalsByTargetClientAsync(
                    targetList,
                    targetType,
                    knownClientIds,
                    effectiveFrom,
                    effectiveTo,
                    effectiveGranularity,
                    token);
                var results = new List<TargetClientUsageBreakdownResponse>();

                foreach (var targetId in targetList)
                {
                    var entries = new List<ClientUsageEntry>();

                    foreach (var clientId in knownClientIds)
                    {
                        if (!totalsByClient.TryGetValue((targetId, clientId), out var totals) || totals.Buckets.Count == 0)
                        {
                            continue;
                        }

                        var grantedCount = totals.Buckets.Values.Sum(bucket => bucket.Granted);
                        var deniedCount = totals.Buckets.Values.Sum(bucket => bucket.Denied);
                        var latestActiveCount = totals.Buckets.Last().Value.Active;

                        if (grantedCount > 0 || deniedCount > 0 || latestActiveCount > 0)
                        {
                            var client = clientMap[clientId];
                            entries.Add(new ClientUsageEntry(client.Id, client.Name, grantedCount, deniedCount, latestActiveCount));
                        }
                    }

                    results.Add(new TargetClientUsageBreakdownResponse(targetId, entries));
                }

                return (IReadOnlyList<TargetClientUsageBreakdownResponse>)results;
            },
            cancellationToken);
    }

    public async Task<ClientSummariesResponse> GetClientSummariesAsync(CancellationToken cancellationToken = default)
        => await _cache.GetOrCreateStatisticsAsync(
            "client-summaries",
            _ => ComputeClientSummariesAsync(cancellationToken),
            cancellationToken);

    public async Task<IReadOnlyList<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        IEnumerable<string> targetIds,
        TargetType targetType,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var targetList = NormalizeIds(targetIds);
        var normalizedClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim();
        IReadOnlyList<string>? clientList = normalizedClientId is null ? null : [normalizedClientId];

        return await _cache.GetOrCreateStatisticsAsync(
            $"historical-usage:{targetType}:{CreateIdsCacheKey(targetList)}:{normalizedClientId}:{from:O}:{to:O}:{granularity}",
            async token =>
            {
                var totalsByTarget = await GetContinuousBucketTotalsByTargetAsync(
                    targetList,
                    targetType,
                    clientList,
                    from,
                    to,
                    granularity,
                    token);
                var results = new List<HistoricalUsageResponse>();

                foreach (var targetId in targetList)
                {
                    var points = new List<HistoricalUsagePoint>();
                    var actualGranularity = granularity;

                    if (totalsByTarget.TryGetValue(targetId, out var totals))
                    {
                        points = ToHistoricalUsagePoints(totals.Buckets);
                        actualGranularity = totals.ActualGranularity;
                    }

                    results.Add(new HistoricalUsageResponse(targetId, targetType, actualGranularity, points));
                }

                return (IReadOnlyList<HistoricalUsageResponse>)results;
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<ClientHistoricalUsageResponse>> GetHistoricalUsageByClientAsync(
        IEnumerable<string> targetIds,
        TargetType targetType,
        IEnumerable<string> clientIds,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var targetList = NormalizeIds(targetIds);
        var clientList = NormalizeIds(clientIds);

        if (targetList.Count == 0 || clientList.Count == 0)
        {
            return [];
        }

        return await _cache.GetOrCreateStatisticsAsync(
            $"historical-usage-by-client:{targetType}:{CreateIdsCacheKey(targetList)}:{CreateIdsCacheKey(clientList)}:{from:O}:{to:O}:{granularity}",
            async token =>
            {
                var totalsByTargetClient = await GetContinuousBucketTotalsByTargetClientAsync(
                    targetList,
                    targetType,
                    clientList,
                    from,
                    to,
                    granularity,
                    token);
                var results = new List<ClientHistoricalUsageResponse>();

                foreach (var targetId in targetList)
                {
                    foreach (var clientId in clientList)
                    {
                        if (!totalsByTargetClient.TryGetValue((targetId, clientId), out var totals) || totals.Buckets.Count == 0)
                        {
                            continue;
                        }

                        results.Add(new ClientHistoricalUsageResponse(
                            targetId,
                            targetType,
                            clientId,
                            totals.ActualGranularity,
                            ToHistoricalUsagePoints(totals.Buckets)));
                    }
                }

                return (IReadOnlyList<ClientHistoricalUsageResponse>)results;
            },
            cancellationToken);
    }

    private async Task<GlobalUsageStatsResponse> ComputeGlobalUsageStatsAsync(CancellationToken cancellationToken)
    {
        var pools = await _poolRepository.GetAllAsync(cancellationToken);
        var totalSlots = 0;
        var acquiredSlots = 0;

        foreach (var pool in pools)
        {
            totalSlots += (int)pool.MaxSlots;
            acquiredSlots += await _allocationDatabase.GetActiveCountAsync(pool.Id, cancellationToken);
        }

        var acquisitionPercentage = totalSlots > 0
            ? Math.Round(acquiredSlots * 100.0 / totalSlots, 1)
            : 0;

        var now = DateTime.UtcNow;
        var recentWindowStart = now.AddMinutes(-5);
        var services = await _serviceRepository.GetAllAsync(cancellationToken);
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
        var serviceIds = NormalizeIds(services.Select(service => service.Id));
        var clientIds = NormalizeIds(clients.Select(client => client.Id));
        var totalsByService = await GetContinuousBucketTotalsByTargetAsync(
            serviceIds,
            TargetType.Service,
            clientIds,
            recentWindowStart,
            now,
            BucketGranularity.FiveMinute,
            cancellationToken);

        var recentRequests = totalsByService.Values
            .Sum(service => service.Buckets.Values.Sum(bucket => bucket.Granted));

        return new GlobalUsageStatsResponse(
            Math.Round(recentRequests / 5.0, 1),
            totalSlots,
            acquiredSlots,
            acquisitionPercentage);
    }

    private async Task<ClientSummariesResponse> ComputeClientSummariesAsync(CancellationToken cancellationToken)
    {
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
        var rows = new List<ClientSummaryRow>();

        foreach (var client in clients)
        {
            var accessibleServices = client.Services.Count(service => service.Value.IsAllowed);
            var totalMaxRequests = client.Services.Values.Where(service => service.RateLimit is not null).Sum(service => service.RateLimit!.MaxRequests);
            var rateLimitCap = totalMaxRequests > 0 ? $"{totalMaxRequests} req/min" : "-";
            var accessiblePools = client.ResourcePools.Count;
            var usedSlots = 0;
            var totalAccessibleSlots = 0;

            foreach (var (poolId, poolSettings) in client.ResourcePools)
            {
                totalAccessibleSlots += (int)poolSettings.MaxSlots;
                usedSlots += await _allocationDatabase.GetActiveCountByClientAsync(poolId, client.Id, cancellationToken);
            }

            rows.Add(new ClientSummaryRow(
                client.Id,
                client.Name,
                accessibleServices,
                rateLimitCap,
                accessiblePools,
                usedSlots,
                totalAccessibleSlots));
        }

        return new ClientSummariesResponse(rows);
    }

    private async Task<IReadOnlyDictionary<string, double>> LoadTargetCapValuesAsync(
        TargetType targetType,
        IReadOnlyCollection<string> targetIds,
        CancellationToken cancellationToken)
    {
        if (targetIds.Count == 0)
        {
            return new Dictionary<string, double>(StringComparer.Ordinal);
        }

        var targetIdSet = targetIds.ToHashSet(StringComparer.Ordinal);
        if (targetType == TargetType.Service)
        {
            var limits = await _globalRateLimitDatabase.GetByTargetTypeAsync(TargetType.Service, cancellationToken);
            return limits
                .Where(limit => targetIdSet.Contains(limit.TargetId))
                .GroupBy(limit => limit.TargetId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => (double)group.First().MaxRequests, StringComparer.Ordinal);
        }

        var pools = await _poolRepository.GetAllAsync(cancellationToken);
        return pools
            .Where(pool => targetIdSet.Contains(pool.Id))
            .ToDictionary(pool => pool.Id, pool => (double)pool.MaxSlots, StringComparer.Ordinal);
    }

    private static List<TimeSeriesPoint> ToUsageTimeSeriesPoints(
        SortedDictionary<DateTime, AggregatedBucketTotals> buckets)
    {
        return buckets
            .Select(kvp => new TimeSeriesPoint(kvp.Key, kvp.Value.Granted))
            .ToList();
    }

    private static List<TimeSeriesPoint> GetUsageTimeSeriesPoints(
        string targetId,
        IReadOnlyCollection<string>? clientIds,
        IReadOnlyDictionary<string, (SortedDictionary<DateTime, AggregatedBucketTotals> Buckets, BucketGranularity ActualGranularity)>? totalsByTarget,
        IReadOnlyDictionary<(string TargetId, string ClientId), (SortedDictionary<DateTime, AggregatedBucketTotals> Buckets, BucketGranularity ActualGranularity)>? totalsByClient)
    {
        if (clientIds is null)
        {
            return totalsByTarget is not null && totalsByTarget.TryGetValue(targetId, out var targetTotals)
                ? ToUsageTimeSeriesPoints(targetTotals.Buckets)
                : [];
        }

        var aggregated = new SortedDictionary<DateTime, double>();
        foreach (var clientId in clientIds)
        {
            if (totalsByClient is null || !totalsByClient.TryGetValue((targetId, clientId), out var clientTotals))
            {
                continue;
            }

            foreach (var bucket in clientTotals.Buckets)
            {
                aggregated[bucket.Key] = aggregated.GetValueOrDefault(bucket.Key) + bucket.Value.Granted;
            }
        }

        return aggregated.Select(kvp => new TimeSeriesPoint(kvp.Key, kvp.Value)).ToList();
    }

    private static IReadOnlyList<string> NormalizeIds(IEnumerable<string> ids)
    {
        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string>? NormalizeOptionalIds(IEnumerable<string>? ids)
    {
        return ids is null ? null : NormalizeIds(ids);
    }

    private static string CreateIdsCacheKey(IReadOnlyCollection<string> ids)
    {
        return string.Join(',', ids);
    }

    private static string CreateOptionalIdsCacheKey(IReadOnlyCollection<string>? ids)
    {
        return ids is null ? "*" : string.Join(',', ids);
    }

    private static BucketGranularity[] GetGranularityFallbackOrder(BucketGranularity requested)
    {
        return requested switch
        {
            BucketGranularity.Second => [BucketGranularity.Second, BucketGranularity.FiveMinute, BucketGranularity.Hour, BucketGranularity.Day],
            BucketGranularity.FiveMinute => [BucketGranularity.FiveMinute, BucketGranularity.Second, BucketGranularity.Hour, BucketGranularity.Day],
            BucketGranularity.Hour => [BucketGranularity.Hour, BucketGranularity.FiveMinute, BucketGranularity.Second, BucketGranularity.Day],
            BucketGranularity.Day => [BucketGranularity.Day, BucketGranularity.Hour, BucketGranularity.FiveMinute, BucketGranularity.Second],
            _ => [requested]
        };
    }


    private static DateTime RoundDownToFiveMinutes(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 5 * 5, 0, DateTimeKind.Utc);
    }
}