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

    public async Task RefreshLatestUsageGaugesAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await _usageSnapshotDatabase.GetAllByGranularityAsync(BucketGranularity.Second, cancellationToken);
        var serviceSnapshots = snapshots.Where(snapshot => snapshot.TargetType == TargetType.Service).ToList();

        if (serviceSnapshots.Count == 0)
        {
            serviceSnapshots = (await _usageSnapshotDatabase.GetAllByGranularityAsync(
                BucketGranularity.FiveMinute,
                cancellationToken))
                .Where(snapshot => snapshot.TargetType == TargetType.Service)
                .ToList();
        }

        var entries = new List<LatestUsageGaugeEntry>();
        foreach (var snapshot in serviceSnapshots)
        {
            var latest = snapshot.Buckets.MaxBy(bucket => bucket.Timestamp);
            if (latest is null)
            {
                continue;
            }

            entries.Add(new LatestUsageGaugeEntry(
                snapshot.TargetId,
                snapshot.ClientId,
                latest.GrantedCount,
                latest.DeniedCount));
        }

        await _precomputedDatabase.UpsertLatestUsageGaugesAsync(
            new LatestUsageGaugesDocument
            {
                Entries = entries,
                UpdatedAtUtc = DateTime.UtcNow
            },
            cancellationToken);
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
