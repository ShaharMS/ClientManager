using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Responses;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

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

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsService"/>.
    /// </summary>
    /// <param name="clientConfigRepository">Repository for client configurations.</param>
    /// <param name="serviceRepository">Repository for service definitions.</param>
    /// <param name="poolRepository">Repository for resource pool definitions.</param>
    /// <param name="allocationRepository">Repository for resource allocation state.</param>
    /// <param name="globalRateLimitRepository">Repository for global rate limits.</param>
    /// <param name="usageSnapshotRepository">Repository for usage snapshot data.</param>
    public StatisticsService(
        IClientConfigurationRepository clientConfigRepository,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationRepository allocationRepository,
        IGlobalRateLimitRepository globalRateLimitRepository,
        IUsageSnapshotRepository usageSnapshotRepository)
    {
        _clientConfigRepository = clientConfigRepository;
        _serviceRepository = serviceRepository;
        _poolRepository = poolRepository;
        _allocationRepository = allocationRepository;
        _globalRateLimitRepository = globalRateLimitRepository;
        _usageSnapshotRepository = usageSnapshotRepository;
    }

    /// <inheritdoc />
    public async Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var pools = await _poolRepository.GetAllAsync(cancellationToken);

        var totalSlots = 0;
        var acquiredSlots = 0;

        foreach (var pool in pools)
        {
            totalSlots += (int)pool.MaxSlots;
            acquiredSlots += await _allocationRepository.GetActiveCountAsync(pool.Id, cancellationToken);
        }

        var acquisitionPercentage = totalSlots > 0
            ? Math.Round(acquiredSlots * 100.0 / totalSlots, 1)
            : 0;

        // Compute request rate from the most recent complete 5-minute bucket
        var latestBucketTime = RoundDownToFiveMinutes(DateTime.UtcNow).AddMinutes(-5);
        var allServiceSnapshots = await _usageSnapshotRepository
            .GetAllByGranularityAsync(BucketGranularity.FiveMinute, cancellationToken);

        var recentRequests = allServiceSnapshots
            .Where(s => s.TargetType == GlobalRateLimitTarget.Service)
            .SelectMany(s => s.Buckets)
            .Where(b => b.Timestamp == latestBucketTime)
            .Sum(b => b.GrantedCount);

        var requestsPerMinute = Math.Round(recentRequests / 5.0, 1);

        return new GlobalUsageStatsResponse(
            RequestsPerMinute: requestsPerMinute,
            TotalPoolSlots: totalSlots,
            AcquiredPoolSlots: acquiredSlots,
            AcquisitionPercentage: acquisitionPercentage);
    }

    /// <inheritdoc />
    public async Task<UsageTimeSeriesResponse> GetUsageTimeSeriesAsync(
        GlobalRateLimitTarget targetType, string targetId, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
        var now = DateTime.UtcNow;
        var effectiveTo = to ?? now;
        var effectiveFrom = from ?? RoundDownToFiveMinutes(now).AddMinutes(-60);

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

        var clientIdSet = clientIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);
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

        return new UsageTimeSeriesResponse(usagePoints, capPoints);
    }

    /// <inheritdoc />
    public async Task<ClientUsageBreakdownResponse> GetClientUsageBreakdownAsync(
        GlobalRateLimitTarget targetType, string targetId, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveGranularity = granularity ?? BucketGranularity.FiveMinute;
        var now = DateTime.UtcNow;

        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);
        var clientIdSet = clientIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var snapshots = await _usageSnapshotRepository.GetByTargetAsync(
            targetId, targetType, effectiveGranularity, cancellationToken);

        var entries = new List<ClientUsageEntry>();

        foreach (var client in clients)
        {
            if (clientIdSet is not null && !clientIdSet.Contains(client.Id))
                continue;

            var snapshot = snapshots.FirstOrDefault(s =>
                string.Equals(s.ClientId, client.Id, StringComparison.OrdinalIgnoreCase));
            if (snapshot is null) continue;

            double count;
            if (from is not null && to is not null)
            {
                count = snapshot.Buckets
                    .Where(b => b.Timestamp >= from && b.Timestamp <= to)
                    .Sum(b => b.GrantedCount);
            }
            else
            {
                var latestBucketTime = RoundDownToFiveMinutes(now).AddMinutes(-5);
                count = snapshot.Buckets
                    .Where(b => b.Timestamp == latestBucketTime)
                    .Sum(b => b.GrantedCount);
            }

            if (count > 0)
            {
                entries.Add(new ClientUsageEntry(client.Id, client.Name, count));
            }
        }

        return new ClientUsageBreakdownResponse(entries);
    }

    /// <inheritdoc />
    public async Task<ClientSummariesResponse> GetClientSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);
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
                usedSlots += await _allocationRepository.GetActiveCountByClientAsync(
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

    /// <inheritdoc />
    public async Task<HistoricalUsageResponse> GetHistoricalUsageAsync(
        string targetId,
        GlobalRateLimitTarget targetType,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
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

        var points = aggregated
            .Select(kvp => new HistoricalUsagePoint(kvp.Key, kvp.Value.granted, kvp.Value.denied, kvp.Value.released, kvp.Value.active))
            .ToList();

        return new HistoricalUsageResponse(targetId, targetType, granularity, points);
    }

    private static DateTime RoundDownToFiveMinutes(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 5 * 5, 0, DateTimeKind.Utc);
    }
}
