using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Api.Services.Storage.Models.Configuration;
using ClientManager.Api.Services.Storage.Interfaces;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage.Implementations.UsageTracking;

/// <summary>
/// Flushes buffered usage events and maintains rolled-up usage snapshots.
/// </summary>
public partial class UsagePersistenceService : BackgroundService
{
    private readonly IAppLogger<UsagePersistenceService> _logger;
    private readonly UsageBuffer _buffer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UsageTrackingOptions _options;
    private readonly IStorageReadCache _cache;

    public UsagePersistenceService(
        IAppLogger<UsagePersistenceService> logger,
        UsageBuffer buffer,
        IServiceScopeFactory scopeFactory,
        IStorageReadCache cache,
        IOptions<UsageTrackingOptions> options)
    {
        _logger = logger;
        _buffer = buffer;
        _scopeFactory = scopeFactory;
        _cache = cache;
        _options = options.Value;
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
                var database = scope.ServiceProvider.GetRequiredService<IUsageSnapshotDatabase>();
                var allocations = scope.ServiceProvider.GetRequiredService<IResourceAllocationDatabase>();
                await FlushBufferAsync(database, allocations, stoppingToken);
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
                var database = scope.ServiceProvider.GetRequiredService<IUsageSnapshotDatabase>();
                var mutated = false;

                mutated |= await RollUpAsync(database, BucketGranularity.Second, BucketGranularity.FiveMinute, TimeSpan.FromMinutes(5), stoppingToken);
                mutated |= await RollUpAsync(database, BucketGranularity.FiveMinute, BucketGranularity.Hour, TimeSpan.FromHours(1), stoppingToken);
                mutated |= await RollUpAsync(database, BucketGranularity.Hour, BucketGranularity.Day, TimeSpan.FromHours(24), stoppingToken);
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
        var groups = counts
            .GroupBy(entry => (entry.Key.ClientId, entry.Key.TargetType, entry.Key.TargetId))
            .ToList();
        var snapshotIds = BuildSnapshotIds(groups, bucketTimestamp);
        var existing = await LoadSnapshotsAsync(database, snapshotIds, cancellationToken);
        var snapshots = new List<UsageSnapshot>(groups.Count);

        foreach (var group in groups)
        {
            snapshots.Add(await BuildUpdatedSnapshotAsync(
                group.ToList(),
                bucketTimestamp,
                existing,
                allocationDatabase,
                cancellationToken));
        }

        await database.UpsertManyAsync(snapshots, cancellationToken);

        _cache.InvalidateStatistics();
        _logger.Debug("Flushed usage counter groups to storage", new { Count = counts.Count });
    }

    private static List<string> BuildSnapshotIds(
        IEnumerable<IGrouping<(string ClientId, TargetType TargetType, string TargetId), KeyValuePair<Models.Entities.UsageBufferKey, long>>> groups,
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

    private async Task<UsageSnapshot> BuildUpdatedSnapshotAsync(
        IReadOnlyList<KeyValuePair<Models.Entities.UsageBufferKey, long>> entries,
        DateTime bucketTimestamp,
        IReadOnlyDictionary<string, UsageSnapshot> existing,
        IResourceAllocationDatabase allocationDatabase,
        CancellationToken cancellationToken)
    {
        var first = entries[0].Key;
        var snapshotId = BuildSnapshotId(first, bucketTimestamp);
        var segmentStart = UsageSegmentHelper.GetSegmentStart(bucketTimestamp, BucketGranularity.Second);
        var snapshot = existing.TryGetValue(snapshotId, out var current)
            ? current
            : CreateSnapshot(snapshotId, first.ClientId, first.TargetId, first.TargetType, BucketGranularity.Second, segmentStart);

        var totals = SumEntries(entries);
        var activeCount = await GetActiveCountAsync(first, allocationDatabase, cancellationToken);
        var buckets = MergeBucket(snapshot.Buckets, bucketTimestamp, totals, activeCount);

        return snapshot with { Buckets = buckets.ToList() };
    }

    private static string BuildSnapshotId(
        Models.Entities.UsageBufferKey key,
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

    private static (long Granted, long Denied, long Released) SumEntries(
        IReadOnlyList<KeyValuePair<Models.Entities.UsageBufferKey, long>> entries)
    {
        var granted = entries.Where(entry => entry.Key.EventType == UsageEventType.Granted).Sum(entry => entry.Value);
        var denied = entries.Where(entry => entry.Key.EventType == UsageEventType.Denied).Sum(entry => entry.Value);
        var released = entries.Where(entry => entry.Key.EventType == UsageEventType.Released).Sum(entry => entry.Value);

        return (granted, denied, released);
    }

    private static IReadOnlyList<UsageBucket> MergeBucket(
        IReadOnlyList<UsageBucket> buckets,
        DateTime bucketTimestamp,
        (long Granted, long Denied, long Released) totals,
        long activeCount)
    {
        var updated = buckets.ToList();
        var index = updated.FindIndex(bucket => bucket.Timestamp == bucketTimestamp);

        if (index >= 0)
        {
            updated[index] = updated[index] with
            {
                GrantedCount = updated[index].GrantedCount + totals.Granted,
                DeniedCount = updated[index].DeniedCount + totals.Denied,
                ReleasedCount = updated[index].ReleasedCount + totals.Released,
                ActiveCount = activeCount
            };

            return updated;
        }

        updated.Add(new UsageBucket
        {
            Timestamp = bucketTimestamp,
            GrantedCount = totals.Granted,
            DeniedCount = totals.Denied,
            ReleasedCount = totals.Released,
            ActiveCount = activeCount
        });

        updated.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
        return updated;
    }

    private static async Task<long> GetActiveCountAsync(
        Models.Entities.UsageBufferKey key,
        IResourceAllocationDatabase allocationDatabase,
        CancellationToken cancellationToken)
    {
        if (key.TargetType != TargetType.ResourcePool)
        {
            return 0;
        }

        return await allocationDatabase.GetActiveCountByClientAsync(
            key.TargetId,
            key.ClientId,
            cancellationToken);
    }

    private static DateTime RoundDownToSecond(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
    }
}