using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Overlays live atomic usage counters onto snapshot-derived bucket totals.
/// </summary>
public partial class UsageStatisticsService
{
    private readonly IUsageCounterDatabase _usageCounterDatabase;
    private readonly UsageTrackingOptions _usageTrackingOptions;

    public UsageStatisticsService(
        IClientConfigurationDatabase clientConfigDatabase,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationDatabase allocationDatabase,
        IGlobalRateLimitDatabase globalRateLimitDatabase,
        IUsageSnapshotDatabase usageSnapshotDatabase,
        IUsageCounterDatabase usageCounterDatabase,
        IStorageReadCache cache,
        IOptions<UsageTrackingOptions> usageTrackingOptions)
    {
        _clientConfigDatabase = clientConfigDatabase;
        _serviceRepository = serviceRepository;
        _poolRepository = poolRepository;
        _allocationDatabase = allocationDatabase;
        _globalRateLimitDatabase = globalRateLimitDatabase;
        _usageSnapshotDatabase = usageSnapshotDatabase;
        _usageCounterDatabase = usageCounterDatabase;
        _cache = cache;
        _usageTrackingOptions = usageTrackingOptions.Value;
    }

    private async Task OverlayUsageCountersByTargetAsync(
        IReadOnlyDictionary<string, ContinuousBucketState> states,
        TargetType targetType,
        IReadOnlyList<string> clientIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var overlayFrom = MaxDateTime(from, DateTime.UtcNow - _usageTrackingOptions.SecondRetention);
        if (overlayFrom > to)
        {
            return;
        }

        var counterKeys = new List<UsageCounterKey>();
        foreach (var targetId in states.Keys)
        {
            foreach (var clientId in clientIds)
            {
                counterKeys.AddRange(_usageCounterDatabase.BuildKeysForRange(
                    clientId,
                    targetType,
                    targetId,
                    overlayFrom,
                    to));
            }
        }

        if (counterKeys.Count == 0)
        {
            return;
        }

        var counterValues = await _usageCounterDatabase.GetBucketCountsAsync(counterKeys, cancellationToken);
        foreach (var (counterKey, value) in counterValues)
        {
            if (value <= 0 || !states.TryGetValue(counterKey.TargetId, out var state))
            {
                continue;
            }

            var bucketTimestamp = MapCounterTimestamp(counterKey.BucketTimestamp, state.ActualGranularity);
            if (bucketTimestamp < from || bucketTimestamp > to)
            {
                continue;
            }

            if (!state.Buckets.TryGetValue(bucketTimestamp, out var totals))
            {
                totals = new AggregatedBucketTotals(0, 0, 0, 0);
            }

            state.Buckets[bucketTimestamp] = counterKey.EventType switch
            {
                UsageEventType.Granted => totals with { Granted = totals.Granted + value },
                UsageEventType.Denied => totals with { Denied = totals.Denied + value },
                UsageEventType.Released => totals with { Released = totals.Released + value },
                _ => totals
            };
            state.FoundAny = true;
        }
    }

    private async Task OverlayUsageCountersByTargetClientAsync(
        IReadOnlyDictionary<(string TargetId, string ClientId), ContinuousBucketState> states,
        TargetType targetType,
        IReadOnlyList<string> clientIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var overlayFrom = MaxDateTime(from, DateTime.UtcNow - _usageTrackingOptions.SecondRetention);
        if (overlayFrom > to)
        {
            return;
        }

        var counterKeys = new List<UsageCounterKey>();
        foreach (var (targetId, clientId) in states.Keys)
        {
            counterKeys.AddRange(_usageCounterDatabase.BuildKeysForRange(
                clientId,
                targetType,
                targetId,
                overlayFrom,
                to));
        }

        if (counterKeys.Count == 0)
        {
            return;
        }

        var counterValues = await _usageCounterDatabase.GetBucketCountsAsync(counterKeys, cancellationToken);
        foreach (var (counterKey, value) in counterValues)
        {
            if (value <= 0)
            {
                continue;
            }

            var stateKey = (counterKey.TargetId, counterKey.ClientId);
            if (!states.TryGetValue(stateKey, out var state))
            {
                continue;
            }

            var bucketTimestamp = MapCounterTimestamp(counterKey.BucketTimestamp, state.ActualGranularity);
            if (bucketTimestamp < from || bucketTimestamp > to)
            {
                continue;
            }

            if (!state.Buckets.TryGetValue(bucketTimestamp, out var totals))
            {
                totals = new AggregatedBucketTotals(0, 0, 0, 0);
            }

            state.Buckets[bucketTimestamp] = counterKey.EventType switch
            {
                UsageEventType.Granted => totals with { Granted = totals.Granted + value },
                UsageEventType.Denied => totals with { Denied = totals.Denied + value },
                UsageEventType.Released => totals with { Released = totals.Released + value },
                _ => totals
            };
            state.FoundAny = true;
        }
    }

    private static DateTime MapCounterTimestamp(DateTime secondTimestamp, BucketGranularity granularity)
    {
        return granularity switch
        {
            BucketGranularity.Second => RoundDownToSecond(secondTimestamp),
            BucketGranularity.FiveMinute => RoundDownToFiveMinutes(secondTimestamp),
            BucketGranularity.Hour => new DateTime(secondTimestamp.Year, secondTimestamp.Month, secondTimestamp.Day, secondTimestamp.Hour, 0, 0, DateTimeKind.Utc),
            BucketGranularity.Day => new DateTime(secondTimestamp.Year, secondTimestamp.Month, secondTimestamp.Day, 0, 0, 0, DateTimeKind.Utc),
            _ => RoundDownToFiveMinutes(secondTimestamp)
        };
    }

    private static DateTime RoundDownToSecond(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
    }

    private static DateTime MaxDateTime(DateTime left, DateTime right) => left > right ? left : right;
}
