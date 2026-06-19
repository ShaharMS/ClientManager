using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Logging;
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
                await FlushBufferAsync(database, stoppingToken);
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
        CancellationToken cancellationToken)
    {
        var counts = _buffer.Drain();
        if (counts.Count == 0)
        {
            return;
        }

        var bucketTimestamp = UsageSegmentHelper.RoundDownToSecond(DateTime.UtcNow);
        var entries = new Dictionary<string, (long amount, TimeSpan window)>(counts.Count, StringComparer.Ordinal);

        foreach (var entry in counts)
        {
            var storageKey = UsageSegmentHelper.BuildUsageCounterKey(
                entry.Key.ClientId,
                entry.Key.TargetType,
                entry.Key.TargetId,
                bucketTimestamp,
                entry.Key.EventType);

            if (entries.TryGetValue(storageKey, out var existing))
            {
                entries[storageKey] = (existing.amount + entry.Value, _options.SecondRetention);
            }
            else
            {
                entries[storageKey] = (entry.Value, _options.SecondRetention);
            }
        }

        await database.IncrementPendingCountersAsync(entries, cancellationToken);

        _cache.InvalidateStatistics();
        _logger.Debug("Flushed usage counter groups to storage", new { Count = counts.Count });
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

    private static DateTime RoundDownToSecond(DateTime utc) => UsageSegmentHelper.RoundDownToSecond(utc);
}
