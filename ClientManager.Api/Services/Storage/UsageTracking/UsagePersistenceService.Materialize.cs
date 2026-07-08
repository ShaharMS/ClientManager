using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.Storage.UsageTracking;

/// <summary>
/// Folds pending second-level usage counters into <see cref="BucketGranularity.Second"/> snapshots
/// so the rollup chain can build longer history.
/// </summary>
public partial class UsagePersistenceService
{
    /// <summary>Fast loop: fold only the second that just completed.</summary>
    private Task<bool> MaterializeLatestSecondAsync(
        IUsageSnapshotDatabase database,
        CancellationToken cancellationToken) =>
        MaterializePendingCountersAsync(database, cancellationToken, latestSecondOnly: true);

    private async Task<bool> MaterializePendingCountersAsync(
        IUsageSnapshotDatabase database,
        CancellationToken cancellationToken,
        bool latestSecondOnly = false)
    {
        var materializeBefore = UsageSegmentHelper.RoundDownToSecond(DateTime.UtcNow);
        var materializeFrom = materializeBefore - _options.SecondRetention - TimeSpan.FromMinutes(1);
        var onlySecond = latestSecondOnly ? materializeBefore.AddSeconds(-1) : (DateTime?)null;

        var counters = await database.GetPendingCounterValuesByPrefixAsync("usage:", cancellationToken);
        if (counters.Count == 0)
        {
            return false;
        }

        var accumulators = new Dictionary<SecondBucketKey, SecondBucketAccumulator>();
        var keysToReset = new List<string>();

        foreach (var (storageKey, value) in counters)
        {
            if (value <= 0 ||
                !UsageSegmentHelper.TryParseUsageCounterKey(
                    storageKey,
                    out var clientId,
                    out var targetType,
                    out var targetId,
                    out var secondTimestamp,
                    out var eventType,
                    out var denialCategory))
            {
                continue;
            }

            if (secondTimestamp >= materializeBefore)
            {
                continue;
            }

            if (secondTimestamp < materializeFrom)
            {
                keysToReset.Add(storageKey);
                continue;
            }

            if (onlySecond is not null && secondTimestamp != onlySecond.Value)
            {
                continue;
            }

            var bucketKey = new SecondBucketKey(clientId, targetType, targetId, secondTimestamp);
            if (!accumulators.TryGetValue(bucketKey, out var accumulator))
            {
                accumulator = new SecondBucketAccumulator(secondTimestamp);
                accumulators[bucketKey] = accumulator;
            }

            accumulator.Apply(eventType, denialCategory, value);
            keysToReset.Add(storageKey);
        }

        var mutated = false;
        if (accumulators.Count > 0)
        {
            foreach (var segmentGroup in accumulators.GroupBy(kvp =>
            {
                var key = kvp.Key;
                var segmentStart = UsageSegmentHelper.GetSegmentStart(key.SecondTimestamp, BucketGranularity.Second);
                return new SegmentKey(key.ClientId, key.TargetType, key.TargetId, segmentStart);
            }))
            {
                var segment = segmentGroup.Key;
                var snapshotId = UsageSegmentHelper.BuildSegmentId(
                    segment.ClientId,
                    segment.TargetType,
                    segment.TargetId,
                    BucketGranularity.Second,
                    segment.SegmentStart);

                var snapshot = await database.GetByIdAsync(snapshotId, cancellationToken)
                    ?? CreateSnapshot(
                        snapshotId,
                        segment.ClientId,
                        segment.TargetId,
                        segment.TargetType,
                        BucketGranularity.Second,
                        segment.SegmentStart);

                var bucketsByTimestamp = snapshot.Buckets.ToDictionary(bucket => bucket.Timestamp);
                foreach (var accumulator in segmentGroup.Select(entry => entry.Value))
                {
                    var materialized = accumulator.ToBucket();
                    if (bucketsByTimestamp.TryGetValue(accumulator.Timestamp, out var existing))
                    {
                        bucketsByTimestamp[accumulator.Timestamp] = MergeMaterializedBucket(existing, materialized);
                    }
                    else
                    {
                        bucketsByTimestamp[accumulator.Timestamp] = materialized;
                    }
                }

                var orderedBuckets = bucketsByTimestamp.Values.OrderBy(bucket => bucket.Timestamp).ToList();
                await database.UpsertAsync(snapshot with { Buckets = orderedBuckets }, cancellationToken);
                mutated = true;
            }
        }

        if (keysToReset.Count > 0)
        {
            await database.ResetPendingCountersAsync(keysToReset, cancellationToken);
        }

        return mutated;
    }

    private static UsageBucket MergeMaterializedBucket(UsageBucket existing, UsageBucket delta)
    {
        var unauth = existing.DeniedUnauthenticatedCount + delta.DeniedUnauthenticatedCount;
        var blocked = existing.DeniedBlockedCount + delta.DeniedBlockedCount;
        var rateLimited = existing.DeniedRateLimitedCount + delta.DeniedRateLimitedCount;
        var capacity = existing.DeniedCapacityLimitedCount + delta.DeniedCapacityLimitedCount;

        return existing with
        {
            GrantedCount = existing.GrantedCount + delta.GrantedCount,
            DeniedUnauthenticatedCount = unauth,
            DeniedBlockedCount = blocked,
            DeniedRateLimitedCount = rateLimited,
            DeniedCapacityLimitedCount = capacity,
            DeniedCount = unauth + blocked + rateLimited + capacity,
            ReleasedCount = existing.ReleasedCount + delta.ReleasedCount,
            ActiveCount = Math.Max(0, existing.ActiveCount + delta.GrantedCount - delta.ReleasedCount)
        };
    }

    private readonly record struct SecondBucketKey(
        string ClientId,
        TargetType TargetType,
        string TargetId,
        DateTime SecondTimestamp);

    private readonly record struct SegmentKey(
        string ClientId,
        TargetType TargetType,
        string TargetId,
        DateTime SegmentStart);

    internal sealed class SecondBucketAccumulator(DateTime timestamp)
    {
        public DateTime Timestamp { get; } = timestamp;
        private long _granted;
        private long _released;
        private long _active;
        private long _unauth;
        private long _blocked;
        private long _rateLimited;
        private long _capacity;

        public void Apply(UsageEventType eventType, UsageDenialCategory? category, long value)
        {
            switch (eventType)
            {
                case UsageEventType.Granted:
                    _granted += value;
                    _active += value;
                    break;
                case UsageEventType.Released:
                    _released += value;
                    _active = Math.Max(0, _active - value);
                    break;
                case UsageEventType.Denied:
                    switch (category)
                    {
                        case UsageDenialCategory.Unauthenticated:
                            _unauth += value;
                            break;
                        case UsageDenialCategory.Blocked:
                            _blocked += value;
                            break;
                        case UsageDenialCategory.RateLimited:
                            _rateLimited += value;
                            break;
                        case UsageDenialCategory.CapacityLimited:
                            _capacity += value;
                            break;
                        default:
                            _blocked += value;
                            break;
                    }

                    break;
            }
        }

        public UsageBucket ToBucket() => new()
        {
            Timestamp = Timestamp,
            GrantedCount = _granted,
            DeniedUnauthenticatedCount = _unauth,
            DeniedBlockedCount = _blocked,
            DeniedRateLimitedCount = _rateLimited,
            DeniedCapacityLimitedCount = _capacity,
            DeniedCount = _unauth + _blocked + _rateLimited + _capacity,
            ReleasedCount = _released,
            ActiveCount = Math.Max(0, _active)
        };
    }
}
