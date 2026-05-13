using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.StorageApi.Services.Implementations.UsageTracking;

/// <summary>
/// Rollup and retention helpers for <see cref="UsagePersistenceService"/>.
/// </summary>
public partial class UsagePersistenceService
{
    private async Task<bool> RollUpAsync(
        IUsageSnapshotDatabase database,
        BucketGranularity sourceGranularity,
        BucketGranularity targetGranularity,
        TimeSpan ageThreshold,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - ageThreshold;
        var snapshots = await database.GetAllByGranularityAsync(sourceGranularity, cancellationToken);
        var mutated = false;

        foreach (var snapshot in snapshots)
        {
            mutated |= await RollUpSnapshotAsync(snapshot, cutoff, targetGranularity, database, cancellationToken);
        }

        return mutated;
    }

    private async Task<bool> RollUpSnapshotAsync(
        UsageSnapshot source,
        DateTime cutoff,
        BucketGranularity targetGranularity,
        IUsageSnapshotDatabase database,
        CancellationToken cancellationToken)
    {
        var bucketsToRollUp = source.Buckets.Where(bucket => bucket.Timestamp < cutoff).ToList();
        if (bucketsToRollUp.Count == 0)
        {
            return false;
        }

        foreach (var group in bucketsToRollUp.GroupBy(bucket => RoundDownToGranularity(bucket.Timestamp, targetGranularity)))
        {
            await UpsertRolledUpSnapshotAsync(
                source,
                group.Key,
                group.ToList(),
                targetGranularity,
                database,
                cancellationToken);
        }

        var remaining = source.Buckets.Where(bucket => bucket.Timestamp >= cutoff).ToList();
        if (remaining.Count == 0)
        {
            await database.DeleteAsync(source.Id, cancellationToken);
            return true;
        }

        await database.UpsertAsync(source with { Buckets = remaining }, cancellationToken);
        return true;
    }

    private async Task UpsertRolledUpSnapshotAsync(
        UsageSnapshot source,
        DateTime targetTimestamp,
        IReadOnlyList<UsageBucket> rolledUpBuckets,
        BucketGranularity targetGranularity,
        IUsageSnapshotDatabase database,
        CancellationToken cancellationToken)
    {
        var segmentStart = UsageSegmentHelper.GetSegmentStart(targetTimestamp, targetGranularity);
        var targetId = UsageSegmentHelper.BuildSegmentId(
            source.ClientId,
            source.TargetType,
            source.TargetId,
            targetGranularity,
            segmentStart);

        var target = await database.GetByIdAsync(targetId, cancellationToken)
            ?? CreateSnapshot(targetId, source.ClientId, source.TargetId, source.TargetType, targetGranularity, segmentStart);

        var mergedBuckets = MergeRolledUpBuckets(target.Buckets, targetTimestamp, rolledUpBuckets);
        await database.UpsertAsync(target with { Buckets = mergedBuckets.ToList() }, cancellationToken);
    }

    private static IReadOnlyList<UsageBucket> MergeRolledUpBuckets(
        IReadOnlyList<UsageBucket> existingBuckets,
        DateTime targetTimestamp,
        IReadOnlyList<UsageBucket> newBuckets)
    {
        var merged = existingBuckets.ToList();
        var totals = SumBuckets(newBuckets);
        var existingIndex = merged.FindIndex(bucket => bucket.Timestamp == targetTimestamp);

        if (existingIndex >= 0)
        {
            merged[existingIndex] = merged[existingIndex] with
            {
                GrantedCount = merged[existingIndex].GrantedCount + totals.Granted,
                DeniedCount = merged[existingIndex].DeniedCount + totals.Denied,
                ReleasedCount = merged[existingIndex].ReleasedCount + totals.Released,
                ActiveCount = Math.Max(merged[existingIndex].ActiveCount, totals.Active)
            };

            return merged;
        }

        merged.Add(new UsageBucket
        {
            Timestamp = targetTimestamp,
            GrantedCount = totals.Granted,
            DeniedCount = totals.Denied,
            ReleasedCount = totals.Released,
            ActiveCount = totals.Active
        });

        merged.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
        return merged;
    }

    private async Task<bool> PruneExpiredAsync(
        IUsageSnapshotDatabase database,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var mutated = false;

        mutated |= await PruneGranularityAsync(database, BucketGranularity.Second, now - _options.SecondRetention, cancellationToken);
        mutated |= await PruneGranularityAsync(database, BucketGranularity.FiveMinute, now - _options.FiveMinuteRetention, cancellationToken);
        mutated |= await PruneGranularityAsync(database, BucketGranularity.Hour, now - _options.HourlyRetention, cancellationToken);
        mutated |= await PruneGranularityAsync(database, BucketGranularity.Day, now - _options.DailyRetention, cancellationToken);

        return mutated;
    }

    private static async Task<bool> PruneGranularityAsync(
        IUsageSnapshotDatabase database,
        BucketGranularity granularity,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        var snapshots = await database.GetAllByGranularityAsync(granularity, cancellationToken);
        var mutated = false;

        foreach (var snapshot in snapshots)
        {
            if (snapshot.SegmentStart.HasValue)
            {
                var segmentEnd = UsageSegmentHelper.GetSegmentEnd(snapshot.SegmentStart.Value, granularity);
                if (segmentEnd <= cutoff)
                {
                    await database.DeleteAsync(snapshot.Id, cancellationToken);
                    mutated = true;
                    continue;
                }
            }

            var remaining = snapshot.Buckets.Where(bucket => bucket.Timestamp >= cutoff).ToList();
            if (remaining.Count == 0)
            {
                await database.DeleteAsync(snapshot.Id, cancellationToken);
                mutated = true;
                continue;
            }

            if (remaining.Count < snapshot.Buckets.Count)
            {
                await database.UpsertAsync(snapshot with { Buckets = remaining }, cancellationToken);
                mutated = true;
            }
        }

        return mutated;
    }

    private static (long Granted, long Denied, long Released, long Active) SumBuckets(
        IReadOnlyList<UsageBucket> buckets)
    {
        return (
            buckets.Sum(bucket => bucket.GrantedCount),
            buckets.Sum(bucket => bucket.DeniedCount),
            buckets.Sum(bucket => bucket.ReleasedCount),
            buckets.Max(bucket => bucket.ActiveCount));
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