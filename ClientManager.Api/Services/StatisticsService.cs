using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Responses;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace ClientManager.Api.Services;

/// <summary>
/// Provides aggregated statistics for the dashboard by reading from data stores
/// and computing usage metrics, time-series data, and client summaries.
/// </summary>
public class StatisticsService : IStatisticsService
{
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationRepository _allocationRepository;
    private readonly IGlobalRateLimitRepository _globalRateLimitRepository;
    private readonly IUsageSnapshotRepository _usageSnapshotRepository;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsService"/>.
    /// </summary>
    /// <param name="clientConfigRepository">Repository for client configurations.</param>
    /// <param name="serviceRepository">Repository for service definitions.</param>
    /// <param name="poolRepository">Repository for resource pool definitions.</param>
    /// <param name="allocationRepository">Repository for resource allocation state.</param>
    /// <param name="globalRateLimitRepository">Repository for global rate limits.</param>
    /// <param name="usageSnapshotRepository">Repository for usage snapshot data.</param>
    /// <param name="cache">In-memory cache for short-lived statistics results.</param>
    public StatisticsService(
        IClientConfigurationRepository clientConfigRepository,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationRepository allocationRepository,
        IGlobalRateLimitRepository globalRateLimitRepository,
        IUsageSnapshotRepository usageSnapshotRepository,
        IMemoryCache cache)
    {
        _clientConfigRepository = clientConfigRepository;
        _serviceRepository = serviceRepository;
        _poolRepository = poolRepository;
        _allocationRepository = allocationRepository;
        _globalRateLimitRepository = globalRateLimitRepository;
        _usageSnapshotRepository = usageSnapshotRepository;
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

    private async Task<GlobalUsageStatsResponse> ComputeGlobalUsageStatsAsync(
        CancellationToken cancellationToken)
    {
        var pools = await _poolRepository.GetAllAsync(cancellationToken);
        var activeCountsByPool = await _allocationRepository.GetActiveCountsByPoolAsync(cancellationToken);

        var totalSlots = 0;
        var acquiredSlots = 0;

        foreach (var pool in pools)
        {
            totalSlots += (int)pool.MaxSlots;
            acquiredSlots += activeCountsByPool.GetValueOrDefault(pool.Id);
        }

        var acquisitionPercentage = totalSlots > 0
            ? Math.Round(acquiredSlots * 100.0 / totalSlots, 1)
            : 0;

        // Compute request rate: prefer per-second buckets for accuracy, fall back to 5-minute
        var now = DateTime.UtcNow;
        var secondCutoff = now.AddSeconds(-60);
        var allSecondSnapshots = await _usageSnapshotRepository
            .GetAllByGranularityAsync(BucketGranularity.Second, cancellationToken);

        var recentSecondRequests = allSecondSnapshots
            .Where(s => s.TargetType == GlobalRateLimitTarget.Service)
            .SelectMany(s => s.Buckets)
            .Where(b => b.Timestamp >= secondCutoff)
            .Sum(b => b.GrantedCount);

        double requestsPerMinute;
        if (recentSecondRequests > 0)
        {
            requestsPerMinute = Math.Round((double)recentSecondRequests, 1);
        }
        else
        {
            var latestBucketTime = RoundDownToFiveMinutes(now).AddMinutes(-5);
            var allServiceSnapshots = await _usageSnapshotRepository
                .GetAllByGranularityAsync(BucketGranularity.FiveMinute, cancellationToken);

            var recentRequests = allServiceSnapshots
                .Where(s => s.TargetType == GlobalRateLimitTarget.Service)
                .SelectMany(s => s.Buckets)
                .Where(b => b.Timestamp == latestBucketTime)
                .Sum(b => b.GrantedCount);

            requestsPerMinute = Math.Round(recentRequests / 5.0, 1);
        }

        return new GlobalUsageStatsResponse(
            RequestsPerMinute: requestsPerMinute,
            TotalPoolSlots: totalSlots,
            AcquiredPoolSlots: acquiredSlots,
            AcquisitionPercentage: acquisitionPercentage);
    }

    /// <inheritdoc />
    public async Task<List<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        GlobalRateLimitTarget targetType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
        var now = DateTime.UtcNow;
        var effectiveTo = to ?? now;
        var effectiveFrom = from ?? RoundDownToFiveMinutes(now).AddMinutes(-60);
        var clientIdSet = clientIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<TargetUsageTimeSeriesResponse>();

        foreach (var targetId in targetIds)
        {
            double capValue = 0;

            if (targetType == GlobalRateLimitTarget.Service)
            {
                var globalLimit = await _globalRateLimitRepository.GetByTargetAsync(
                    targetId, GlobalRateLimitTarget.Service, cancellationToken);
                capValue = globalLimit?.MaxRequests ?? 0;
            }
            else
            {
                var pool = await _poolRepository.GetByIdAsync(targetId, cancellationToken);
                capValue = pool?.MaxSlots ?? 0;
            }

            var snapshots = await _usageSnapshotRepository.GetByTargetAsync(
                targetId, targetType, effectiveGranularity, cancellationToken);

            if (clientIdSet is not null)
            {
                snapshots = snapshots.Where(s => clientIdSet.Contains(s.ClientId)).ToList();
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

    /// <inheritdoc />
    public async Task<List<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        GlobalRateLimitTarget targetType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
        var now = DateTime.UtcNow;
        var granularityFallbackOrder = GetGranularityFallbackOrder(effectiveGranularity);

        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);
        var clientIdSet = clientIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<TargetClientUsageBreakdownResponse>();

        foreach (var targetId in targetIds)
        {
            var entries = new List<ClientUsageEntry>();

            foreach (var tryGranularity in granularityFallbackOrder)
            {
                var snapshots = await _usageSnapshotRepository.GetByTargetAsync(
                    targetId, targetType, tryGranularity, cancellationToken);

                entries.Clear();

                foreach (var client in clients)
                {
                    if (clientIdSet is not null && !clientIdSet.Contains(client.Id))
                        continue;

                    var snapshot = snapshots.FirstOrDefault(s =>
                        string.Equals(s.ClientId, client.Id, StringComparison.OrdinalIgnoreCase));
                    if (snapshot is null) continue;

                    var filteredBuckets = (from is not null && to is not null)
                        ? snapshot.Buckets.Where(b => b.Timestamp >= from && b.Timestamp <= to).ToList()
                        : snapshot.Buckets
                            .Where(b => b.Timestamp == RoundDownToFiveMinutes(now).AddMinutes(-5))
                            .ToList();

                    var grantedCount = filteredBuckets.Sum(b => b.GrantedCount);
                    var deniedCount = filteredBuckets.Sum(b => b.DeniedCount);
                    var latestActiveCount = filteredBuckets
                        .OrderBy(b => b.Timestamp)
                        .Select(b => b.ActiveCount)
                        .LastOrDefault();

                    double count;
                    count = targetType == GlobalRateLimitTarget.ResourcePool
                        ? filteredBuckets.Select(b => (double)b.ActiveCount).DefaultIfEmpty(0).Max()
                        : grantedCount;

                    if (count > 0 || deniedCount > 0 || latestActiveCount > 0)
                    {
                        entries.Add(new ClientUsageEntry(
                            client.Id,
                            client.Name,
                            count,
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

    private async Task<ClientSummariesResponse> ComputeClientSummariesAsync(
        CancellationToken cancellationToken)
    {
        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);
        var activeCountsByPoolAndClient = await _allocationRepository.GetActiveCountsByPoolAndClientAsync(cancellationToken);
        var rows = new List<ClientSummaryRow>();

        foreach (var client in clients)
        {
            var accessibleServices = client.Services.Count(s => s.Value.IsAllowed);

            // Sum rate limit caps across all services that have one
            var totalMaxRequests = client.Services.Values
                .Where(s => s.RateLimit is not null)
                .Sum(s => s.RateLimit!.MaxRequests);

            var rateLimitCap = totalMaxRequests > 0
                ? $"{totalMaxRequests} req/min"
                : "—";

            var accessiblePools = client.ResourcePools.Count;

            var usedSlots = 0;
            var totalAccessibleSlots = 0;
            foreach (var (poolId, poolSettings) in client.ResourcePools)
            {
                totalAccessibleSlots += (int)poolSettings.MaxSlots;
                usedSlots += activeCountsByPoolAndClient.GetValueOrDefault((poolId, client.Id));
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

    /// <inheritdoc />
    public async Task<List<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        IEnumerable<string> targetIds,
        GlobalRateLimitTarget targetType,
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
        GlobalRateLimitTarget targetType,
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
                var snapshot = await _usageSnapshotRepository.GetByClientAndTargetAsync(
                    clientId, targetId, targetType, granularity, cancellationToken);
                snapshots = snapshot is not null ? [snapshot] : [];
            }
            else
            {
                snapshots = await _usageSnapshotRepository.GetByTargetAsync(
                    targetId, targetType, granularity, cancellationToken);
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
