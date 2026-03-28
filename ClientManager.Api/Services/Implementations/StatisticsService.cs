using ClientManager.Api.Services.Interfaces;
using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

using Microsoft.Extensions.Caching.Memory;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Provides aggregated statistics by reading from data stores and computing usage metrics,
/// time-series data, and client summaries. Uses segment-based lookups and atomic counters
/// to avoid full-collection scans on performance-sensitive paths. Results are cached briefly
/// (5 seconds) to absorb repeated polling from dashboard clients.
/// </summary>
public class StatisticsService : IStatisticsService
{
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IGlobalRateLimitDatabase _globalRateLimitDatabase;
    private readonly IUsageSnapshotDatabase _usageSnapshotDatabase;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsService"/>.
    /// </summary>
    /// <param name="clientConfigDatabase">Database for client configurations.</param>
    /// <param name="serviceRepository">Repository for service definitions.</param>
    /// <param name="poolRepository">Repository for resource pool definitions.</param>
    /// <param name="allocationDatabase">Database for resource allocation state.</param>
    /// <param name="globalRateLimitDatabase">Database for global rate limits.</param>
    /// <param name="usageSnapshotDatabase">Database for usage snapshot data.</param>
    /// <param name="cache">In-memory cache for short-lived statistics results.</param>
    public StatisticsService(
        IClientConfigurationDatabase clientConfigDatabase,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationDatabase allocationDatabase,
        IGlobalRateLimitDatabase globalRateLimitDatabase,
        IUsageSnapshotDatabase usageSnapshotDatabase,
        IMemoryCache cache)
    {
        _clientConfigDatabase = clientConfigDatabase;
        _serviceRepository = serviceRepository;
        _poolRepository = poolRepository;
        _allocationDatabase = allocationDatabase;
        _globalRateLimitDatabase = globalRateLimitDatabase;
        _usageSnapshotDatabase = usageSnapshotDatabase;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(
        CancellationToken cancellationToken = default)
    {
        const string cacheKey = "stats:global-usage";
        if (_cache.TryGetValue(cacheKey, out GlobalUsageStatsResponse? cached) && cached is not null)
            return cached;

        var result = await ComputeGlobalUsageStatsAsync(cancellationToken);
        _cache.Set(cacheKey, result, CacheDuration);
        return result;
    }

    /// <summary>
    /// Computes global usage stats using maintained atomic counters for allocation counts
    /// (avoiding full allocation scans) and segment-range lookups for recent request rates
    /// (avoiding loading every second-granularity snapshot in the system).
    /// </summary>
    private async Task<GlobalUsageStatsResponse> ComputeGlobalUsageStatsAsync(
        CancellationToken cancellationToken)
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
                service.Id, TargetType.Service, BucketGranularity.Second,
                secondCutoff, now, cancellationToken);

            recentSecondRequests += snapshots
                .SelectMany(s => s.Buckets)
                .Where(b => b.Timestamp >= secondCutoff)
                .Sum(b => b.GrantedCount);
        }

        double requestsPerMinute;
        if (recentSecondRequests > 0)
        {
            requestsPerMinute = Math.Round((double)recentSecondRequests, 1);
        }
        else
        {
            var latestBucketTime = RoundDownToFiveMinutes(now).AddMinutes(-5);
            var fiveMinFrom = latestBucketTime;
            var fiveMinTo = latestBucketTime.AddMinutes(5);

            long recentRequests = 0;
            foreach (var service in services)
            {
                var snapshots = await _usageSnapshotDatabase.GetByTargetAndRangeAsync(
                    service.Id, TargetType.Service, BucketGranularity.FiveMinute,
                    fiveMinFrom, fiveMinTo, cancellationToken);

                recentRequests += snapshots
                    .SelectMany(s => s.Buckets)
                    .Where(b => b.Timestamp == latestBucketTime)
                    .Sum(b => b.GrantedCount);
            }

            requestsPerMinute = Math.Round(recentRequests / 5.0, 1);
        }

        return new GlobalUsageStatsResponse(
            RequestsPerMinute: requestsPerMinute,
            TotalPoolSlots: totalSlots,
            AcquiredPoolSlots: acquiredSlots,
            AcquisitionPercentage: acquisitionPercentage);
    }

    /// <summary>
    /// Builds per-target time-series using <c>GetByTargetAndRangeAsync</c>, which fetches
    /// only the segment documents overlapping the requested time range instead of loading
    /// the entire snapshot collection.
    /// </summary>
    public async Task<IReadOnlyList<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        TargetType targetType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
        var now = DateTime.UtcNow;
        var effectiveTo = to ?? now;
        var effectiveFrom = from ?? RoundDownToFiveMinutes(now).AddHours(-1);
        var clientIdSet = clientIds?.ToHashSet();

        var results = new List<TargetUsageTimeSeriesResponse>();

        foreach (var targetId in targetIds)
        {
            double capValue = 0;

            if (targetType == TargetType.Service)
            {
                var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(
                    targetId, TargetType.Service, cancellationToken);
                capValue = globalLimit?.MaxRequests ?? 0;
            }
            else
            {
                var pool = await _poolRepository.GetByIdAsync(targetId, cancellationToken);
                capValue = pool?.MaxSlots ?? 0;
            }

            var snapshots = await _usageSnapshotDatabase.GetByTargetAndRangeAsync(
                targetId, targetType, effectiveGranularity,
                effectiveFrom, effectiveTo, cancellationToken);

            if (clientIdSet is not null)
            {
                snapshots = [.. snapshots.Where(s => clientIdSet.Contains(s.ClientId))];
            }

            var aggregated = new SortedDictionary<DateTime, double>();
            foreach (var snapshot in snapshots)
            {
                foreach (var bucket in snapshot.Buckets)
                {
                    if (bucket.Timestamp < effectiveFrom || bucket.Timestamp > effectiveTo)
                        continue;

                    if (aggregated.TryGetValue(bucket.Timestamp, out var existing))
                        aggregated[bucket.Timestamp] = existing + bucket.GrantedCount;
                    else
                        aggregated[bucket.Timestamp] = bucket.GrantedCount;
                }
            }

            var usagePoints = aggregated
                .Select(kvp => new TimeSeriesPoint(kvp.Key, kvp.Value)).ToList();
            var capPoints = usagePoints
                .Select(p => new TimeSeriesPoint(p.Timestamp, capValue)).ToList();

            results.Add(new TargetUsageTimeSeriesResponse(targetId, usagePoints, capPoints));
        }

        return results;
    }

    /// <summary>
    /// Builds per-client usage breakdowns using direct <c>GetByClientTargetAndSegmentAsync</c>
    /// lookups per (client, target, segment) instead of loading all snapshots per target and
    /// iterating all clients (the prior N×M nested-loop pattern).
    /// </summary>
    public async Task<IReadOnlyList<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        TargetType targetType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
        var now = DateTime.UtcNow;
        var granularityFallbackOrder = GetGranularityFallbackOrder(effectiveGranularity);

        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
        var clientIdSet = clientIds?.ToHashSet();

        var results = new List<TargetClientUsageBreakdownResponse>();

        foreach (var targetId in targetIds)
        {
            var entries = new List<ClientUsageEntry>();

            foreach (var tryGranularity in granularityFallbackOrder)
            {
                entries.Clear();

                var effectiveFrom = from ?? RoundDownToFiveMinutes(now).AddMinutes(-5);
                var effectiveTo = to ?? now;

                foreach (var client in clients)
                {
                    if (clientIdSet is not null && !clientIdSet.Contains(client.Id))
                        continue;

                    var segmentStarts = UsageSegmentHelper.EnumerateSegmentStarts(
                        effectiveFrom, effectiveTo, tryGranularity);

                    var allBuckets = new List<UsageBucket>();
                    foreach (var segmentStart in segmentStarts)
                    {
                        var snapshot = await _usageSnapshotDatabase.GetByClientTargetAndSegmentAsync(
                            client.Id, targetId, targetType, tryGranularity, segmentStart, cancellationToken);
                        if (snapshot is not null)
                        {
                            allBuckets.AddRange(snapshot.Buckets);
                        }
                    }

                    if (allBuckets.Count == 0) continue;

                    var filteredBuckets = allBuckets
                        .Where(b => b.Timestamp >= effectiveFrom && b.Timestamp <= effectiveTo);

                    var grantedCount = filteredBuckets.Sum(b => b.GrantedCount);
                    var deniedCount = filteredBuckets.Sum(b => b.DeniedCount);
                    var latestActiveCount = filteredBuckets
                        .OrderBy(b => b.Timestamp)
                        .Select(b => b.ActiveCount)
                        .LastOrDefault();

                    if (grantedCount > 0 || deniedCount > 0 || latestActiveCount > 0)
                    {
                        entries.Add(new ClientUsageEntry(
                            client.Id,
                            client.Name,
                            grantedCount,
                            deniedCount,
                            latestActiveCount));
                    }
                }

                if (entries.Count > 0)
                    break; // Found data at this granularity
            }

            results.Add(new TargetClientUsageBreakdownResponse(targetId, entries));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<ClientSummariesResponse> GetClientSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        const string cacheKey = "stats:client-summaries";
        if (_cache.TryGetValue(cacheKey, out ClientSummariesResponse? cached) && cached is not null)
            return cached;

        var result = await ComputeClientSummariesAsync(cancellationToken);
        _cache.Set(cacheKey, result, CacheDuration);
        return result;
    }

    /// <summary>
    /// Computes per-client summary rows using individual counter reads per (pool, client) pair
    /// instead of scanning the entire allocation collection and grouping in memory.
    /// </summary>
    private async Task<ClientSummariesResponse> ComputeClientSummariesAsync(
        CancellationToken cancellationToken)
    {
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
        var rows = new List<ClientSummaryRow>();

        foreach (var client in clients)
        {
            var accessibleServices = client.Services.Count(s => s.Value.IsAllowed);

            var totalMaxRequests = client.Services.Values
                .Where(s => s.RateLimit is not null)
                .Sum(s => s.RateLimit!.MaxRequests);

            var rateLimitCap = totalMaxRequests > 0
                ? $"{totalMaxRequests} req/min"
                : "-";

            var accessiblePools = client.ResourcePools.Count;

            var usedSlots = 0;
            var totalAccessibleSlots = 0;
            foreach (var (poolId, poolSettings) in client.ResourcePools)
            {
                totalAccessibleSlots += (int)poolSettings.MaxSlots;
                usedSlots += await _allocationDatabase.GetActiveCountByClientAsync(
                    poolId, client.Id, cancellationToken);
            }

            rows.Add(new ClientSummaryRow(
                ClientId: client.Id,
                DisplayName: client.Name,
                AccessibleServices: accessibleServices,
                TotalRateLimitCap: rateLimitCap,
                AccessiblePools: accessiblePools,
                UsedSlots: usedSlots,
                TotalAccessibleSlots: totalAccessibleSlots));
        }

        return new ClientSummariesResponse(rows);
    }

    /// <summary>
    /// Retrieves historical usage data using segment-range lookups. The granularity fallback
    /// logic is unchanged — it still tries multiple granularities until data is found — but
    /// each attempt is now a bounded segment lookup instead of a full-collection scan.
    /// </summary>
    public async Task<IReadOnlyList<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        IEnumerable<string> targetIds,
        TargetType targetType,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var results = new List<HistoricalUsageResponse>();

        // Define fallback order: start from requested granularity, try coarser if no data
        var granularityFallbackOrder = GetGranularityFallbackOrder(granularity);

        foreach (var targetId in targetIds)
        {
            var (points, actualGranularity) = await FetchHistoricalPointsWithFallbackAsync(
                targetId, targetType, clientId, from, to, granularityFallbackOrder, cancellationToken);

            results.Add(new HistoricalUsageResponse(targetId, targetType, actualGranularity, points));
        }

        return results;
    }

    private static BucketGranularity[] GetGranularityFallbackOrder(BucketGranularity requested)
    {
        // Return fallback order starting from requested granularity down to finest available
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
                        clientId, targetId, targetType, granularity, segmentStart, cancellationToken);
                    if (snapshot is not null)
                        collected.Add(snapshot);
                }
                snapshots = collected;
            }
            else
            {
                snapshots = await _usageSnapshotDatabase.GetByTargetAndRangeAsync(
                    targetId, targetType, granularity, from, to, cancellationToken);
            }

            var aggregated = new SortedDictionary<DateTime, (long granted, long denied, long released, long active)>();

            foreach (var snapshot in snapshots)
            {
                foreach (var bucket in snapshot.Buckets)
                {
                    if (bucket.Timestamp < from || bucket.Timestamp > to)
                        continue;

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
                        aggregated[bucket.Timestamp] = (bucket.GrantedCount, bucket.DeniedCount, bucket.ReleasedCount, bucket.ActiveCount);
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

        // No data found in any granularity
        return ([], fallbackOrder[0]);
    }

    private static DateTime RoundDownToFiveMinutes(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 5 * 5, 0, DateTimeKind.Utc);
    }
}
