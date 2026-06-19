using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage.UsageTracking;

/// <summary>
/// Flushes buffered usage events and maintains rolled-up usage snapshots.
/// </summary>
public partial class UsagePersistenceService : BackgroundService
{
    private readonly IAppLogger<UsagePersistenceService> _logger;
    private readonly UsageBuffer _buffer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UsageTrackingOptions _options;
    private readonly BackgroundWorkersOptions _workerOptions;
    private readonly IStorageReadCache _cache;

    public UsagePersistenceService(
        IAppLogger<UsagePersistenceService> logger,
        UsageBuffer buffer,
        IServiceScopeFactory scopeFactory,
        IStorageReadCache cache,
        IOptions<UsageTrackingOptions> options,
        IOptions<BackgroundWorkersOptions> workerOptions)
    {
        _logger = logger;
        _buffer = buffer;
        _scopeFactory = scopeFactory;
        _cache = cache;
        _options = options.Value;
        _workerOptions = workerOptions.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fastLoop = RunFastLoopAsync(stoppingToken);
        var slowLoop = RunSlowLoopAsync(stoppingToken);
        await Task.WhenAll(fastLoop, slowLoop);
    }

    private async Task RunFastLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_options.SecondFlushInterval, stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var usageCounters = scope.ServiceProvider.GetRequiredService<IUsageCounterDatabase>();
                var database = scope.ServiceProvider.GetRequiredService<IUsageSnapshotDatabase>();
                var allocations = scope.ServiceProvider.GetRequiredService<IResourceAllocationDatabase>();
                await FlushBufferAsync(usageCounters, database, allocations, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.Error("Error in fast usage persistence cycle", exception: exception);
            }
        }
    }

    private async Task RunSlowLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_options.FlushInterval, stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var leaderLock = scope.ServiceProvider.GetRequiredService<IDistributedLeaderLock>();
                await using var lease = await leaderLock.TryAcquireAsync("usage-rollup", stoppingToken);
                if (lease is null && _workerOptions.RequireLeaderLock)
                {
                    continue;
                }

                var database = scope.ServiceProvider.GetRequiredService<IUsageSnapshotDatabase>();
                var usageCounters = scope.ServiceProvider.GetRequiredService<IUsageCounterDatabase>();
                var mutated = false;

                mutated |= await RollUpAsync(database, usageCounters, BucketGranularity.Second, BucketGranularity.FiveMinute, TimeSpan.FromMinutes(5), stoppingToken);
                mutated |= await RollUpAsync(database, usageCounters, BucketGranularity.FiveMinute, BucketGranularity.Hour, TimeSpan.FromHours(1), stoppingToken);
                mutated |= await RollUpAsync(database, usageCounters, BucketGranularity.Hour, BucketGranularity.Day, TimeSpan.FromHours(24), stoppingToken);
                mutated |= await PruneExpiredAsync(database, stoppingToken);

                if (mutated)
                {
                    _cache.InvalidateStatistics();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.Error("Error in slow usage persistence cycle", exception: exception);
            }
        }
    }

    private async Task FlushBufferAsync(
        IUsageCounterDatabase usageCounters,
        IUsageSnapshotDatabase database,
        IResourceAllocationDatabase allocationDatabase,
        CancellationToken cancellationToken)
    {
        var counts = _buffer.Drain();
        if (counts.Count == 0)
        {
            return;
        }

        var bucketTimestamp = RoundDownToSecond(DateTime.UtcNow);
        var counterDeltas = new Dictionary<UsageCounterKey, long>();

        foreach (var entry in counts)
        {
            var key = entry.Key;
            var counterKey = new UsageCounterKey(
                key.ClientId,
                key.TargetType,
                key.TargetId,
                BucketGranularity.Second,
                bucketTimestamp,
                key.EventType);
            counterDeltas[counterKey] = entry.Value;
        }

        await usageCounters.IncrementBucketCountsAsync(counterDeltas, cancellationToken);

        var poolGroups = counts
            .Where(entry => entry.Key.TargetType == TargetType.ResourcePool)
            .GroupBy(entry => (entry.Key.ClientId, entry.Key.TargetId))
            .ToList();

        if (poolGroups.Count > 0)
        {
            await FlushActiveCountsAsync(poolGroups, bucketTimestamp, database, allocationDatabase, cancellationToken);
        }

        _cache.InvalidateStatistics();
        _logger.Debug("Flushed usage counter groups to storage", new { Count = counts.Count });
    }

    private static async Task FlushActiveCountsAsync(
        List<IGrouping<(string ClientId, string TargetId), KeyValuePair<UsageBufferKey, long>>> poolGroups,
        DateTime bucketTimestamp,
        IUsageSnapshotDatabase database,
        IResourceAllocationDatabase allocationDatabase,
        CancellationToken cancellationToken)
    {
        var snapshotIds = poolGroups
            .Select(group => BuildSnapshotId(group.Key.ClientId, TargetType.ResourcePool, group.Key.TargetId, bucketTimestamp))
            .ToList();
        var existing = await LoadSnapshotsAsync(database, snapshotIds, cancellationToken);
        var snapshots = new List<UsageSnapshot>(poolGroups.Count);

        foreach (var group in poolGroups)
        {
            var (clientId, targetId) = group.Key;
            var snapshotId = BuildSnapshotId(clientId, TargetType.ResourcePool, targetId, bucketTimestamp);
            var segmentStart = UsageSegmentHelper.GetSegmentStart(bucketTimestamp, BucketGranularity.Second);
            var snapshot = existing.TryGetValue(snapshotId, out var current)
                ? current
                : CreateSnapshot(snapshotId, clientId, targetId, TargetType.ResourcePool, BucketGranularity.Second, segmentStart);

            var activeCount = await allocationDatabase.GetActiveCountByClientAsync(targetId, clientId, cancellationToken);
            var buckets = MergeActiveCountOnly(snapshot.Buckets, bucketTimestamp, activeCount);
            snapshots.Add(snapshot with { Buckets = buckets.ToList() });
        }

        await database.UpsertManyAsync(snapshots, cancellationToken);
    }

    private static List<string> BuildSnapshotIds(
        IEnumerable<IGrouping<(string ClientId, TargetType TargetType, string TargetId), KeyValuePair<UsageBufferKey, long>>> groups,
        DateTime bucketTimestamp)
    {
        return groups.Select(group => BuildSnapshotId(group.Key, bucketTimestamp)).ToList();
    }

    private static async Task<Dictionary<string, UsageSnapshot>> LoadSnapshotsAsync(
        IUsageSnapshotDatabase database,
        IReadOnlyCollection<string> snapshotIds,
        CancellationToken cancellationToken)
    {
        var snapshots = await database.GetByIdsAsync(snapshotIds, cancellationToken);
        return snapshots.ToDictionary(snapshot => snapshot.Id, StringComparer.Ordinal);
    }

    private static string BuildSnapshotId(
        (string ClientId, TargetType TargetType, string TargetId) key,
        DateTime bucketTimestamp)
    {
        return UsageSegmentHelper.BuildSegmentId(
            key.ClientId,
            key.TargetType,
            key.TargetId,
            BucketGranularity.Second,
            UsageSegmentHelper.GetSegmentStart(bucketTimestamp, BucketGranularity.Second));
    }

    private static string BuildSnapshotId(
        string clientId,
        TargetType targetType,
        string targetId,
        DateTime bucketTimestamp)
    {
        return UsageSegmentHelper.BuildSegmentId(
            clientId,
            targetType,
            targetId,
            BucketGranularity.Second,
            UsageSegmentHelper.GetSegmentStart(bucketTimestamp, BucketGranularity.Second));
    }

    private static UsageSnapshot CreateSnapshot(
        string snapshotId,
        string clientId,
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        DateTime segmentStart)
    {
        return new UsageSnapshot
        {
            Id = snapshotId,
            ClientId = clientId,
            TargetId = targetId,
            TargetType = targetType,
            Granularity = granularity,
            SegmentStart = segmentStart,
            Buckets = []
        };
    }

    private static IReadOnlyList<UsageBucket> MergeActiveCountOnly(
        IReadOnlyList<UsageBucket> buckets,
        DateTime bucketTimestamp,
        long activeCount)
    {
        var updated = buckets.ToList();
        var index = updated.FindIndex(bucket => bucket.Timestamp == bucketTimestamp);

        if (index >= 0)
        {
            updated[index] = updated[index] with
            {
                ActiveCount = Math.Max(updated[index].ActiveCount, activeCount)
            };

            return updated;
        }

        updated.Add(new UsageBucket
        {
            Timestamp = bucketTimestamp,
            GrantedCount = 0,
            DeniedCount = 0,
            ReleasedCount = 0,
            ActiveCount = activeCount
        });

        updated.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
        return updated;
    }

    private static DateTime RoundDownToSecond(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
    }
}
