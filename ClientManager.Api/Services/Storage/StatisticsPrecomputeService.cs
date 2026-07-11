using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Utils;
using ClientManager.Api.Services.Interfaces;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Maintains precomputed overview and gauge documents during usage persistence.
/// </summary>
public sealed class StatisticsPrecomputeService : IStatisticsPrecomputeService
{
    private readonly IStatisticsPrecomputedDatabase _precomputedDatabase;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IUsageSnapshotDatabase _usageSnapshotDatabase;

    public StatisticsPrecomputeService(
        IStatisticsPrecomputedDatabase precomputedDatabase,
        IClientConfigurationDatabase clientConfigDatabase,
        IEntityRepository<ResourcePool> poolRepository,
        IEntityRepository<Service> serviceRepository,
        IResourceAllocationDatabase allocationDatabase,
        IUsageSnapshotDatabase usageSnapshotDatabase)
    {
        _precomputedDatabase = precomputedDatabase;
        _clientConfigDatabase = clientConfigDatabase;
        _poolRepository = poolRepository;
        _serviceRepository = serviceRepository;
        _allocationDatabase = allocationDatabase;
        _usageSnapshotDatabase = usageSnapshotDatabase;
    }

    public async Task RefreshOverviewSummaryAsync(CancellationToken cancellationToken = default)
    {
        var pools = await _poolRepository.GetAllAsync(cancellationToken);
        var poolIds = pools.Select(pool => pool.Id).ToArray();
        var poolCounts = await _allocationDatabase.GetActiveCountsForPoolsAsync(poolIds, cancellationToken);

        var totalSlots = 0;
        var acquiredSlots = 0;
        foreach (var pool in pools)
        {
            totalSlots += (int)pool.MaxSlots;
            acquiredSlots += poolCounts.GetValueOrDefault(pool.Id);
        }

        var acquisitionPercentage = totalSlots > 0
            ? Math.Round(acquiredSlots * 100.0 / totalSlots, 1)
            : 0;

        var now = DateTime.UtcNow;
        var recentFrom = now.AddMinutes(-5);
        var services = await _serviceRepository.GetAllAsync(cancellationToken);
        var serviceIds = services.Select(service => service.Id).ToArray();
        var clientIds = await ResolveServiceClientIdsAsync(serviceIds, cancellationToken);
        var snapshots = serviceIds.Length == 0 || clientIds.Count == 0
            ? []
            : await _usageSnapshotDatabase.GetByTargetsAndRangeAsync(
                serviceIds,
                TargetType.Service,
                BucketGranularity.FiveMinute,
                recentFrom,
                now,
                clientIds,
                cancellationToken);

        long recentGranted = 0;
        var storageDuration = TimeSpan.FromMinutes(5);
        foreach (var snapshot in snapshots)
        {
            foreach (var bucket in snapshot.Buckets)
            {
                if (BucketGranularityHelper.OverlapsRange(bucket.Timestamp, storageDuration, recentFrom, now))
                {
                    recentGranted += bucket.GrantedCount;
                }
            }
        }

        await _precomputedDatabase.UpsertOverviewSummaryAsync(
            new StatisticsOverviewSummary
            {
                RequestsPerMinute = Math.Round(recentGranted / 5.0, 1),
                TotalPoolSlots = totalSlots,
                AcquiredPoolSlots = acquiredSlots,
                AcquisitionPercentage = acquisitionPercentage,
                UpdatedAtUtc = now
            },
            cancellationToken);
    }

    public async Task UpdateLatestUsageGaugesAsync(
        IReadOnlyCollection<ServiceClientGaugeKey> dirtyPairs,
        CancellationToken cancellationToken = default)
    {
        if (dirtyPairs.Count == 0)
        {
            return;
        }

        var existing = await _precomputedDatabase.GetLatestUsageGaugesAsync(cancellationToken);
        var entriesByKey = existing?.Entries.ToDictionary(
            entry => (entry.ServiceId, entry.ClientId),
            entry => entry)
            ?? new Dictionary<(string ServiceId, string ClientId), LatestUsageGaugeEntry>();

        var counters = await _usageSnapshotDatabase.GetPendingCounterValuesByPrefixAsync("usage:", cancellationToken);
        var now = DateTime.UtcNow;
        var overlayFrom = now.AddMinutes(-5);

        foreach (var pair in dirtyPairs)
        {
            var (granted, denied) = await ResolveGaugeCountsAsync(
                pair.ServiceId,
                pair.ClientId,
                counters,
                overlayFrom,
                now,
                cancellationToken);

            entriesByKey[(pair.ServiceId, pair.ClientId)] = new LatestUsageGaugeEntry(
                pair.ServiceId,
                pair.ClientId,
                granted,
                denied);
        }

        await _precomputedDatabase.UpsertLatestUsageGaugesAsync(
            new LatestUsageGaugesDocument
            {
                Entries = entriesByKey.Values.ToList(),
                UpdatedAtUtc = now
            },
            cancellationToken);
    }

    private async Task<(long Granted, long Denied)> ResolveGaugeCountsAsync(
        string serviceId,
        string clientId,
        IReadOnlyDictionary<string, long> counters,
        DateTime overlayFrom,
        DateTime now,
        CancellationToken cancellationToken)
    {
        long granted = 0;
        long denied = 0;
        var overlayStart = UsageSegmentHelper.RoundDownToSecond(overlayFrom);
        var overlayEnd = UsageSegmentHelper.RoundDownToSecond(now);
        DateTime? latestSecond = null;

        foreach (var (storageKey, value) in counters)
        {
            if (value <= 0 ||
                !UsageSegmentHelper.TryParseUsageCounterKey(
                    storageKey,
                    out var parsedClientId,
                    out var targetType,
                    out var targetId,
                    out var secondTimestamp,
                    out _,
                    out _))
            {
                continue;
            }

            if (targetType != TargetType.Service ||
                !string.Equals(parsedClientId, clientId, StringComparison.Ordinal) ||
                !string.Equals(targetId, serviceId, StringComparison.Ordinal) ||
                secondTimestamp < overlayStart ||
                secondTimestamp > overlayEnd)
            {
                continue;
            }

            if (latestSecond is null || secondTimestamp > latestSecond)
            {
                latestSecond = secondTimestamp;
            }
        }

        if (latestSecond is not null)
        {
            foreach (var (storageKey, value) in counters)
            {
                if (value <= 0 ||
                    !UsageSegmentHelper.TryParseUsageCounterKey(
                        storageKey,
                        out var parsedClientId,
                        out var targetType,
                        out var targetId,
                        out var secondTimestamp,
                        out var eventType,
                        out _))
                {
                    continue;
                }

                if (targetType != TargetType.Service ||
                    !string.Equals(parsedClientId, clientId, StringComparison.Ordinal) ||
                    !string.Equals(targetId, serviceId, StringComparison.Ordinal) ||
                    secondTimestamp != latestSecond)
                {
                    continue;
                }

                if (eventType == UsageEventType.Granted)
                {
                    granted += value;
                }
                else if (eventType == UsageEventType.Denied)
                {
                    denied += value;
                }
            }
        }

        if (granted > 0 || denied > 0)
        {
            return (granted, denied);
        }

        return await ReadLatestSnapshotCountsAsync(serviceId, clientId, cancellationToken);
    }

    private async Task<(long Granted, long Denied)> ReadLatestSnapshotCountsAsync(
        string serviceId,
        string clientId,
        CancellationToken cancellationToken)
    {
        var segmentStart = UsageSegmentHelper.RoundDownToSecond(DateTime.UtcNow.AddMinutes(-5));
        var snapshot = await _usageSnapshotDatabase.GetByClientTargetAndSegmentAsync(
            clientId,
            serviceId,
            TargetType.Service,
            BucketGranularity.Second,
            segmentStart,
            cancellationToken);

        var latest = snapshot?.Buckets.MaxBy(bucket => bucket.Timestamp);
        return latest is null ? (0, 0) : (latest.GrantedCount, latest.DeniedCount);
    }

    private async Task<IReadOnlyList<string>> ResolveServiceClientIdsAsync(
        IReadOnlyList<string> serviceIds,
        CancellationToken cancellationToken)
    {
        var serviceIdSet = serviceIds.ToHashSet(StringComparer.Ordinal);
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
        return clients
            .Where(client => client.Services.Keys.Any(serviceIdSet.Contains))
            .Select(client => client.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
