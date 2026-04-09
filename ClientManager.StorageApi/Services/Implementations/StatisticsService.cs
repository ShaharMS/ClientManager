using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.StorageApi.Services.Interfaces;

namespace ClientManager.StorageApi.Services.Implementations;

/// <summary>
/// Builds dashboard and export read models inside the storage-owning host.
/// </summary>
public class StatisticsService : IStatisticsService
{
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IGlobalRateLimitDatabase _globalRateLimitDatabase;
    private readonly IUsageSnapshotDatabase _usageSnapshotDatabase;
    private readonly IStorageReadCache _cache;

    public StatisticsService(
        IClientConfigurationDatabase clientConfigDatabase,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationDatabase allocationDatabase,
        IGlobalRateLimitDatabase globalRateLimitDatabase,
        IUsageSnapshotDatabase usageSnapshotDatabase,
        IStorageReadCache cache)
    {
        _clientConfigDatabase = clientConfigDatabase;
        _serviceRepository = serviceRepository;
        _poolRepository = poolRepository;
        _allocationDatabase = allocationDatabase;
        _globalRateLimitDatabase = globalRateLimitDatabase;
        _usageSnapshotDatabase = usageSnapshotDatabase;
        _cache = cache;
    }

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
        var targetList = targetIds.ToList();
        var clientList = clientIds?.ToList();

        return await _cache.GetOrCreateStatisticsAsync(
            $"usage-timeseries:{targetType}:{string.Join(',', targetList)}:{string.Join(',', clientList ?? [])}:{from:O}:{to:O}:{granularity}",
            async token =>
            {
                var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
                var now = DateTime.UtcNow;
                var effectiveTo = to ?? now;
                var effectiveFrom = from ?? RoundDownToFiveMinutes(now).AddHours(-1);
                var clientIdSet = clientList?.ToHashSet();
                var results = new List<TargetUsageTimeSeriesResponse>();

                foreach (var targetId in targetList)
                {
                    double capValue = 0;

                    if (targetType == TargetType.Service)
                    {
                        var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(
                            targetId,
                            TargetType.Service,
                            token);
                        capValue = globalLimit?.MaxRequests ?? 0;
                    }
                    else
                    {
                        var pool = await _poolRepository.GetByIdAsync(targetId, token);
                        capValue = pool?.MaxSlots ?? 0;
                    }

                    var snapshots = await _usageSnapshotDatabase.GetByTargetAndRangeAsync(
                        targetId,
                        targetType,
                        effectiveGranularity,
                        effectiveFrom,
                        effectiveTo,
                        token);

                    if (clientIdSet is not null)
                    {
                        snapshots = [.. snapshots.Where(snapshot => clientIdSet.Contains(snapshot.ClientId))];
                    }

                    var aggregated = new SortedDictionary<DateTime, double>();
                    foreach (var snapshot in snapshots)
                    {
                        foreach (var bucket in snapshot.Buckets)
                        {
                            if (bucket.Timestamp < effectiveFrom || bucket.Timestamp > effectiveTo)
                            {
                                continue;
                            }

                            aggregated[bucket.Timestamp] = aggregated.GetValueOrDefault(bucket.Timestamp) + bucket.GrantedCount;
                        }
                    }

                    var usagePoints = aggregated.Select(kvp => new TimeSeriesPoint(kvp.Key, kvp.Value)).ToList();
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
        var targetList = targetIds.ToList();
        var clientList = clientIds?.ToList();

        return await _cache.GetOrCreateStatisticsAsync(
            $"client-usage-breakdown:{targetType}:{string.Join(',', targetList)}:{string.Join(',', clientList ?? [])}:{from:O}:{to:O}:{granularity}",
            async token =>
            {
                var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
                var now = DateTime.UtcNow;
                var fallbackOrder = GetGranularityFallbackOrder(effectiveGranularity);
                var clients = await _clientConfigDatabase.GetAllAsync(token);
                var clientIdSet = clientList?.ToHashSet();
                var results = new List<TargetClientUsageBreakdownResponse>();

                foreach (var targetId in targetList)
                {
                    var entries = new List<ClientUsageEntry>();

                    foreach (var tryGranularity in fallbackOrder)
                    {
                        entries.Clear();

                        var effectiveFrom = from ?? RoundDownToFiveMinutes(now).AddMinutes(-5);
                        var effectiveTo = to ?? now;

                        foreach (var client in clients)
                        {
                            if (clientIdSet is not null && !clientIdSet.Contains(client.Id))
                            {
                                continue;
                            }

                            var segmentStarts = UsageSegmentHelper.EnumerateSegmentStarts(effectiveFrom, effectiveTo, tryGranularity);
                            var allBuckets = new List<UsageBucket>();

                            foreach (var segmentStart in segmentStarts)
                            {
                                var snapshot = await _usageSnapshotDatabase.GetByClientTargetAndSegmentAsync(
                                    client.Id,
                                    targetId,
                                    targetType,
                                    tryGranularity,
                                    segmentStart,
                                    token);

                                if (snapshot is not null)
                                {
                                    allBuckets.AddRange(snapshot.Buckets);
                                }
                            }

                            if (allBuckets.Count == 0)
                            {
                                continue;
                            }

                            var filteredBuckets = allBuckets.Where(bucket => bucket.Timestamp >= effectiveFrom && bucket.Timestamp <= effectiveTo);
                            var grantedCount = filteredBuckets.Sum(bucket => bucket.GrantedCount);
                            var deniedCount = filteredBuckets.Sum(bucket => bucket.DeniedCount);
                            var latestActiveCount = filteredBuckets.OrderBy(bucket => bucket.Timestamp).Select(bucket => bucket.ActiveCount).LastOrDefault();

                            if (grantedCount > 0 || deniedCount > 0 || latestActiveCount > 0)
                            {
                                entries.Add(new ClientUsageEntry(client.Id, client.Name, grantedCount, deniedCount, latestActiveCount));
                            }
                        }

                        if (entries.Count > 0)
                        {
                            break;
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
        var targetList = targetIds.ToList();

        return await _cache.GetOrCreateStatisticsAsync(
            $"historical-usage:{targetType}:{string.Join(',', targetList)}:{clientId}:{from:O}:{to:O}:{granularity}",
            async token =>
            {
                var results = new List<HistoricalUsageResponse>();
                var fallbackOrder = GetGranularityFallbackOrder(granularity);

                foreach (var targetId in targetList)
                {
                    var (points, actualGranularity) = await FetchHistoricalPointsWithFallbackAsync(
                        targetId,
                        targetType,
                        clientId,
                        from,
                        to,
                        fallbackOrder,
                        token);

                    results.Add(new HistoricalUsageResponse(targetId, targetType, actualGranularity, points));
                }

                return (IReadOnlyList<HistoricalUsageResponse>)results;
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
        var secondCutoff = now.AddSeconds(-60);
        var services = await _serviceRepository.GetAllAsync(cancellationToken);

        long recentSecondRequests = 0;
        foreach (var service in services)
        {
            var snapshots = await _usageSnapshotDatabase.GetByTargetAndRangeAsync(
                service.Id,
                TargetType.Service,
                BucketGranularity.Second,
                secondCutoff,
                now,
                cancellationToken);

            recentSecondRequests += snapshots
                .SelectMany(snapshot => snapshot.Buckets)
                .Where(bucket => bucket.Timestamp >= secondCutoff)
                .Sum(bucket => bucket.GrantedCount);
        }

        if (recentSecondRequests > 0)
        {
            return new GlobalUsageStatsResponse(recentSecondRequests, totalSlots, acquiredSlots, acquisitionPercentage);
        }

        var latestBucketTime = RoundDownToFiveMinutes(now).AddMinutes(-5);
        long recentRequests = 0;

        foreach (var service in services)
        {
            var snapshots = await _usageSnapshotDatabase.GetByTargetAndRangeAsync(
                service.Id,
                TargetType.Service,
                BucketGranularity.FiveMinute,
                latestBucketTime,
                latestBucketTime.AddMinutes(5),
                cancellationToken);

            recentRequests += snapshots
                .SelectMany(snapshot => snapshot.Buckets)
                .Where(bucket => bucket.Timestamp == latestBucketTime)
                .Sum(bucket => bucket.GrantedCount);
        }

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

    private async Task<(List<HistoricalUsagePoint> Points, BucketGranularity ActualGranularity)> FetchHistoricalPointsWithFallbackAsync(
        string targetId,
        TargetType targetType,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity[] fallbackOrder,
        CancellationToken cancellationToken)
    {
        foreach (var granularity in fallbackOrder)
        {
            IReadOnlyList<UsageSnapshot> snapshots;

            if (clientId is not null)
            {
                var segmentStarts = UsageSegmentHelper.EnumerateSegmentStarts(from, to, granularity);
                var collected = new List<UsageSnapshot>();

                foreach (var segmentStart in segmentStarts)
                {
                    var snapshot = await _usageSnapshotDatabase.GetByClientTargetAndSegmentAsync(
                        clientId,
                        targetId,
                        targetType,
                        granularity,
                        segmentStart,
                        cancellationToken);

                    if (snapshot is not null)
                    {
                        collected.Add(snapshot);
                    }
                }

                snapshots = collected;
            }
            else
            {
                snapshots = await _usageSnapshotDatabase.GetByTargetAndRangeAsync(
                    targetId,
                    targetType,
                    granularity,
                    from,
                    to,
                    cancellationToken);
            }

            var aggregated = new SortedDictionary<DateTime, (long granted, long denied, long released, long active)>();

            foreach (var snapshot in snapshots)
            {
                foreach (var bucket in snapshot.Buckets)
                {
                    if (bucket.Timestamp < from || bucket.Timestamp > to)
                    {
                        continue;
                    }

                    if (aggregated.TryGetValue(bucket.Timestamp, out var existing))
                    {
                        aggregated[bucket.Timestamp] = (
                            existing.granted + bucket.GrantedCount,
                            existing.denied + bucket.DeniedCount,
                            existing.released + bucket.ReleasedCount,
                            existing.active + bucket.ActiveCount);
                    }
                    else
                    {
                        aggregated[bucket.Timestamp] = (
                            bucket.GrantedCount,
                            bucket.DeniedCount,
                            bucket.ReleasedCount,
                            bucket.ActiveCount);
                    }
                }
            }

            if (aggregated.Count > 0)
            {
                var points = aggregated
                    .Select(kvp => new HistoricalUsagePoint(kvp.Key, kvp.Value.granted, kvp.Value.denied, kvp.Value.released, kvp.Value.active))
                    .ToList();

                return (points, granularity);
            }
        }

        return ([], fallbackOrder[0]);
    }

    private static DateTime RoundDownToFiveMinutes(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 5 * 5, 0, DateTimeKind.Utc);
    }
}