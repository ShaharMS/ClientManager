using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.StorageApi.Models.Configuration;
using Microsoft.Extensions.Options;

namespace ClientManager.StorageApi.Services.Implementations.UsageTracking;

/// <summary>
/// Flushes buffered usage events and maintains rolled-up usage snapshots.
/// </summary>
public partial class UsagePersistenceService : BackgroundService
{
    private readonly IAppLogger<UsagePersistenceService> _logger;
    private readonly UsageBuffer _buffer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UsageTrackingOptions _options;

    public UsagePersistenceService(
        IAppLogger<UsagePersistenceService> logger,
        UsageBuffer buffer,
        IServiceScopeFactory scopeFactory,
        IOptions<UsageTrackingOptions> options)
    {
        _logger = logger;
        _buffer = buffer;
        _scopeFactory = scopeFactory;
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
                _logger.Error("Error in fast usage persistence cycle", exception);
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
                await RollUpAsync(database, BucketGranularity.Second, BucketGranularity.FiveMinute, TimeSpan.FromMinutes(5), stoppingToken);
                await RollUpAsync(database, BucketGranularity.FiveMinute, BucketGranularity.Hour, TimeSpan.FromHours(1), stoppingToken);
                await RollUpAsync(database, BucketGranularity.Hour, BucketGranularity.Day, TimeSpan.FromHours(24), stoppingToken);
                await PruneExpiredAsync(database, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.Error("Error in slow usage persistence cycle", exception);
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
        var groups = counts.GroupBy(entry => (entry.Key.ClientId, entry.Key.TargetType, entry.Key.TargetId));

        foreach (var group in groups)
        {
            await FlushGroupAsync(group.ToList(), bucketTimestamp, database, allocationDatabase, cancellationToken);
        }

        _logger.Debug("Flushed usage counter groups to storage", new { Count = counts.Count });
    }

    private async Task FlushGroupAsync(
        IReadOnlyList<KeyValuePair<Models.Entities.UsageBufferKey, long>> entries,
        DateTime bucketTimestamp,
        IUsageSnapshotDatabase database,
        IResourceAllocationDatabase allocationDatabase,
        CancellationToken cancellationToken)
    {
        var first = entries[0].Key;
        var segmentStart = UsageSegmentHelper.GetSegmentStart(bucketTimestamp, BucketGranularity.Second);
        var snapshotId = UsageSegmentHelper.BuildSegmentId(
            first.ClientId,
            first.TargetType,
            first.TargetId,
            BucketGranularity.Second,
            segmentStart);

        var snapshot = await database.GetByIdAsync(snapshotId, cancellationToken)
            ?? CreateSnapshot(snapshotId, first.ClientId, first.TargetId, first.TargetType, BucketGranularity.Second, segmentStart);

        var totals = SumEntries(entries);
        var activeCount = await GetActiveCountAsync(first, allocationDatabase, cancellationToken);
        var buckets = MergeBucket(snapshot.Buckets, bucketTimestamp, totals, activeCount);

        await database.UpsertAsync(snapshot with { Buckets = buckets.ToList() }, cancellationToken);
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