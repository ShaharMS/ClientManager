using ClientManager.Api.Services.Interfaces;
using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Configuration.Storage;
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
                var dirtyGaugePairs = await FlushBufferAsync(database, stoppingToken);
                await MaterializeLatestSecondAsync(database, stoppingToken);

                if (dirtyGaugePairs.Count > 0)
                {
                    using var precomputeScope = _scopeFactory.CreateScope();
                    var precompute = precomputeScope.ServiceProvider.GetRequiredService<IStatisticsPrecomputeService>();
                    await precompute.UpdateLatestUsageGaugesAsync(dirtyGaugePairs, stoppingToken);
                }
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

                mutated |= await MaterializePendingCountersAsync(database, stoppingToken);
                mutated |= await RollUpAsync(database, BucketGranularity.Second, BucketGranularity.OneMinute, TimeSpan.FromMinutes(1), stoppingToken);
                mutated |= await RollUpAsync(database, BucketGranularity.OneMinute, BucketGranularity.FiveMinute, TimeSpan.FromHours(1), stoppingToken);
                mutated |= await RollUpAsync(database, BucketGranularity.FiveMinute, BucketGranularity.Hour, TimeSpan.FromHours(1), stoppingToken);
                mutated |= await RollUpAsync(database, BucketGranularity.Hour, BucketGranularity.Day, TimeSpan.FromHours(24), stoppingToken);
                mutated |= await PruneExpiredAsync(database, stoppingToken);

                if (mutated)
                {
                    _cache.InvalidateStatisticsClosed();
                    using var precomputeScope = _scopeFactory.CreateScope();
                    var precompute = precomputeScope.ServiceProvider.GetRequiredService<IStatisticsPrecomputeService>();
                    await precompute.RefreshOverviewSummaryAsync(stoppingToken);
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

    private async Task<IReadOnlyList<ServiceClientGaugeKey>> FlushBufferAsync(
        IUsageSnapshotDatabase database,
        CancellationToken cancellationToken)
    {
        var counts = _buffer.Drain();
        if (counts.Count == 0)
        {
            return [];
        }

        var bucketTimestamp = UsageSegmentHelper.RoundDownToSecond(DateTime.UtcNow);
        var entries = new Dictionary<string, (long amount, TimeSpan window)>(counts.Count, StringComparer.Ordinal);
        var dirtyGaugePairs = new HashSet<ServiceClientGaugeKey>();

        foreach (var entry in counts)
        {
            if (entry.Key.TargetType == TargetType.Service)
            {
                dirtyGaugePairs.Add(new ServiceClientGaugeKey(entry.Key.TargetId, entry.Key.ClientId));
            }

            var storageKey = entry.Key.EventType == UsageEventType.Denied
                ? UsageSegmentHelper.BuildUsageCounterKey(
                    entry.Key.ClientId,
                    entry.Key.TargetType,
                    entry.Key.TargetId,
                    bucketTimestamp,
                    UsageEventType.Denied,
                    entry.Key.DenialCategory ?? UsageDenialCategory.Blocked)
                : UsageSegmentHelper.BuildUsageCounterKey(
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

        _logger.Debug("Flushed usage counter groups to storage", new { Count = counts.Count });
        return dirtyGaugePairs.ToList();
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
