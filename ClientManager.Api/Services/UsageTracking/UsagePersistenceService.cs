using ClientManager.Api.Models;
using ClientManager.DataAccess.Implementations;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.UsageTracking;

/// <summary>
/// Background service that periodically flushes the in-memory usage buffer to persistent storage,
/// rolls up fine-grained buckets into coarser granularities, and prunes expired data.
/// </summary>
public class UsagePersistenceService : BackgroundService
{
    private readonly ILogger<UsagePersistenceService> _logger;
    private readonly UsageBuffer _buffer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UsageTrackingOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="UsagePersistenceService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="buffer">The shared in-memory usage buffer.</param>
    /// <param name="scopeFactory">Factory for creating service scopes.</param>
    /// <param name="options">Usage tracking configuration options.</param>
    public UsagePersistenceService(
        ILogger<UsagePersistenceService> logger,
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
                var repository = scope.ServiceProvider.GetRequiredService<IUsageSnapshotRepository>();
                var allocationRepository = scope.ServiceProvider.GetRequiredService<IResourceAllocationRepository>();

                await FlushBufferAsync(repository, allocationRepository, BucketGranularity.Second, RoundDownToSecond, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in fast usage persistence cycle");
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
                var repository = scope.ServiceProvider.GetRequiredService<IUsageSnapshotRepository>();

                await RollUpAsync(repository, BucketGranularity.Second, BucketGranularity.FiveMinute, TimeSpan.FromMinutes(5), stoppingToken);
                await RollUpAsync(repository, BucketGranularity.FiveMinute, BucketGranularity.Hour, TimeSpan.FromHours(1), stoppingToken);
                await RollUpAsync(repository, BucketGranularity.Hour, BucketGranularity.Day, TimeSpan.FromHours(24), stoppingToken);
                await PruneExpiredAsync(repository, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in slow usage persistence cycle");
            }
        }
    }

    private async Task FlushBufferAsync(
        IUsageSnapshotRepository repository,
        IResourceAllocationRepository allocationRepository,
        BucketGranularity granularity,
        Func<DateTime, DateTime> roundDown,
        CancellationToken cancellationToken)
    {
        var counts = _buffer.Drain();
        if (counts.Count == 0)
            return;

        var bucketTimestamp = roundDown(DateTime.UtcNow);

        var grouped = counts
            .GroupBy(c => (c.Key.ClientId, c.Key.TargetType, c.Key.TargetId))
            .ToList();

        foreach (var group in grouped)
        {
            var (clientId, targetType, targetId) = group.Key;
            var id = UsageSnapshotRepository.BuildId(clientId, targetType, targetId, granularity);

            var snapshot = await repository.GetByIdAsync(id, cancellationToken)
                ?? new UsageSnapshot
                {
                    Id = id,
                    ClientId = clientId,
                    TargetId = targetId,
                    TargetType = targetType,
                    Granularity = granularity,
                    Buckets = new List<UsageBucket>()
                };

            long granted = 0;
            long denied = 0;
            long released = 0;
            foreach (var entry in group)
            {
                if (entry.Key.EventType == UsageEventType.Granted)
                    granted += entry.Value;
                else if (entry.Key.EventType == UsageEventType.Denied)
                    denied += entry.Value;
                else if (entry.Key.EventType == UsageEventType.Released)
                    released += entry.Value;
            }

            long activeCount = 0;
            if (targetType == GlobalRateLimitTarget.ResourcePool)
            {
                activeCount = await allocationRepository.GetActiveCountByClientAsync(targetId, clientId, cancellationToken);
            }

            var buckets = snapshot.Buckets.ToList();
            var existing = buckets.FindIndex(b => b.Timestamp == bucketTimestamp);
            if (existing >= 0)
            {
                buckets[existing] = buckets[existing] with
                {
                    GrantedCount = buckets[existing].GrantedCount + granted,
                    DeniedCount = buckets[existing].DeniedCount + denied,
                    ReleasedCount = buckets[existing].ReleasedCount + released,
                    ActiveCount = activeCount
                };
            }
            else
            {
                buckets.Add(new UsageBucket
                {
                    Timestamp = bucketTimestamp,
                    GrantedCount = granted,
                    DeniedCount = denied,
                    ReleasedCount = released,
                    ActiveCount = activeCount
                });
                buckets.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            }

            await repository.UpsertAsync(snapshot with { Buckets = buckets }, cancellationToken);
        }

        _logger.LogDebug("Flushed {Count} usage counter groups to storage", grouped.Count);
    }

    private async Task RollUpAsync(
        IUsageSnapshotRepository repository,
        BucketGranularity sourceGranularity,
        BucketGranularity targetGranularity,
        TimeSpan ageThreshold,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - ageThreshold;
        var sourceSnapshots = await repository.GetAllByGranularityAsync(sourceGranularity, cancellationToken);

        foreach (var source in sourceSnapshots)
        {
            var bucketsToRollUp = source.Buckets.Where(b => b.Timestamp < cutoff).ToList();
            if (bucketsToRollUp.Count == 0)
                continue;

            var targetId = UsageSnapshotRepository.BuildId(
                source.ClientId, source.TargetType, source.TargetId, targetGranularity);

            var target = await repository.GetByIdAsync(targetId, cancellationToken)
                ?? new UsageSnapshot
                {
                    Id = targetId,
                    ClientId = source.ClientId,
                    TargetId = source.TargetId,
                    TargetType = source.TargetType,
                    Granularity = targetGranularity,
                    Buckets = new List<UsageBucket>()
                };

            var targetBuckets = target.Buckets.ToList();

            var rollUpGroups = bucketsToRollUp
                .GroupBy(b => RoundDownToGranularity(b.Timestamp, targetGranularity))
                .ToList();

            foreach (var group in rollUpGroups)
            {
                var targetTimestamp = group.Key;
                var totalGranted = group.Sum(b => b.GrantedCount);
                var totalDenied = group.Sum(b => b.DeniedCount);
                var totalReleased = group.Sum(b => b.ReleasedCount);
                var maxActive = group.Max(b => b.ActiveCount);

                var existingIndex = targetBuckets.FindIndex(b => b.Timestamp == targetTimestamp);
                if (existingIndex >= 0)
                {
                    targetBuckets[existingIndex] = targetBuckets[existingIndex] with
                    {
                        GrantedCount = targetBuckets[existingIndex].GrantedCount + totalGranted,
                        DeniedCount = targetBuckets[existingIndex].DeniedCount + totalDenied,
                        ReleasedCount = targetBuckets[existingIndex].ReleasedCount + totalReleased,
                        ActiveCount = Math.Max(targetBuckets[existingIndex].ActiveCount, maxActive)
                    };
                }
                else
                {
                    targetBuckets.Add(new UsageBucket
                    {
                        Timestamp = targetTimestamp,
                        GrantedCount = totalGranted,
                        DeniedCount = totalDenied,
                        ReleasedCount = totalReleased,
                        ActiveCount = maxActive
                    });
                }
            }

            targetBuckets.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            await repository.UpsertAsync(target with { Buckets = targetBuckets }, cancellationToken);

            var remaining = source.Buckets.Where(b => b.Timestamp >= cutoff).ToList();
            await repository.UpsertAsync(source with { Buckets = remaining }, cancellationToken);
        }
    }

    private async Task PruneExpiredAsync(IUsageSnapshotRepository repository, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await PruneGranularityAsync(repository, BucketGranularity.Second, now - _options.SecondRetention, cancellationToken);
        await PruneGranularityAsync(repository, BucketGranularity.FiveMinute, now - _options.FiveMinuteRetention, cancellationToken);
        await PruneGranularityAsync(repository, BucketGranularity.Hour, now - _options.HourlyRetention, cancellationToken);
        await PruneGranularityAsync(repository, BucketGranularity.Day, now - _options.DailyRetention, cancellationToken);
    }

    private static async Task PruneGranularityAsync(
        IUsageSnapshotRepository repository,
        BucketGranularity granularity,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        var snapshots = await repository.GetAllByGranularityAsync(granularity, cancellationToken);

        foreach (var snapshot in snapshots)
        {
            var remaining = snapshot.Buckets.Where(b => b.Timestamp >= cutoff).ToList();
            if (remaining.Count < snapshot.Buckets.Count)
            {
                await repository.UpsertAsync(snapshot with { Buckets = remaining }, cancellationToken);
            }
        }
    }

    private static DateTime RoundDownToSecond(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, 0, DateTimeKind.Utc);
    }

    private static DateTime RoundDownToFiveMinutes(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 5 * 5, 0, DateTimeKind.Utc);
    }

    private static DateTime RoundDownToGranularity(DateTime utc, BucketGranularity granularity)
    {
        return granularity switch
        {
            BucketGranularity.Second => RoundDownToSecond(utc),
            BucketGranularity.FiveMinute => RoundDownToFiveMinutes(utc),
            BucketGranularity.Hour => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc),
            BucketGranularity.Day => new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc),
            _ => RoundDownToFiveMinutes(utc)
        };
    }
}
