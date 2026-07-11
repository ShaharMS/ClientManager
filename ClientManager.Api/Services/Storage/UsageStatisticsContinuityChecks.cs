namespace ClientManager.Api.Services.Storage;

using ClientManager.Api.Services.Storage.UsageTracking;
using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

/// <summary>ponytail: assert-only guard for continuity boundary math; run via dotnet run -- --usage-continuity-check.</summary>
internal static class UsageStatisticsContinuityChecks
{
    internal static int Run()
    {
        var fiveMinuteStart = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var exclusiveAfter = fiveMinuteStart + TimeSpan.FromMinutes(5);

        if (new DateTime(2026, 1, 1, 10, 1, 0, DateTimeKind.Utc) >= exclusiveAfter)
        {
            return 1;
        }

        if (new DateTime(2026, 1, 1, 10, 6, 0, DateTimeKind.Utc) < exclusiveAfter)
        {
            return 2;
        }

        var legacy = new UsageBucket
        {
            Timestamp = DateTime.UtcNow,
            DeniedCount = 5,
            DeniedBlockedCount = 2
        };
        var breakdown = legacy.GetDeniedBreakdown();
        if (breakdown.Unauth + breakdown.Blocked + breakdown.RateLimited + breakdown.Capacity != legacy.DeniedCount)
        {
            return 4;
        }

        var hourStart = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var earliestFiveMinute = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        if (hourStart + TimeSpan.FromHours(1) > earliestFiveMinute)
        {
            return 3;
        }

        var activeAfterGrant = DeriveActiveCount(0, granted: 2, released: 0, storedActive: 0);
        var activeAfterRelease = DeriveActiveCount(activeAfterGrant, granted: 1, released: 1, storedActive: 0);
        if (activeAfterGrant != 2 || activeAfterRelease != 2)
        {
            return 5;
        }

        var folded = new UsagePersistenceService.SecondBucketAccumulator(
            new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        folded.Apply(UsageEventType.Granted, null, 4);
        folded.Apply(UsageEventType.Denied, UsageDenialCategory.RateLimited, 2);
        var bucket = folded.ToBucket();
        if (bucket.GrantedCount != 4 || bucket.DeniedRateLimitedCount != 2 || bucket.DeniedCount != 2)
        {
            return 6;
        }

        var materializeBefore = new DateTime(2026, 1, 1, 10, 0, 2, DateTimeKind.Utc);
        var materializeFrom = materializeBefore - TimeSpan.FromMinutes(6) - TimeSpan.FromMinutes(1);
        var onlySecond = materializeBefore.AddSeconds(-1);
        var eligible = new DateTime(2026, 1, 1, 10, 0, 1, DateTimeKind.Utc);
        var stale = new DateTime(2026, 1, 1, 9, 50, 0, DateTimeKind.Utc);
        if (stale >= materializeFrom ||
            eligible < materializeFrom ||
            eligible >= materializeBefore ||
            eligible != onlySecond ||
            materializeBefore < eligible)
        {
            return 7;
        }

        var rpmFrom = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var rpmTo = rpmFrom.AddMinutes(5);
        if (StatisticsGranularityPicker.PickForRange(rpmFrom, rpmTo, bucketCount: 5) != BucketGranularity.Second)
        {
            return 8;
        }

        var totals = new Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, BucketTotals>>
        {
            [("svc", "client")] = new SortedDictionary<DateTime, BucketTotals>
            {
                [rpmFrom] = new BucketTotals(60, 0, 0, 0, 0, 0, 0),
                [rpmFrom.AddMinutes(1)] = new BucketTotals(60, 0, 0, 0, 0, 0, 0),
                [rpmFrom.AddMinutes(2)] = new BucketTotals(60, 0, 0, 0, 0, 0, 0),
                [rpmFrom.AddMinutes(3)] = new BucketTotals(60, 0, 0, 0, 0, 0, 0),
                [rpmFrom.AddMinutes(4)] = new BucketTotals(60, 0, 0, 0, 0, 0, 0)
            }
        };
        if (StatisticsRequestsPerMinuteCalculator.Compute(totals, rpmFrom, rpmTo, BucketGranularity.Second, windowMinutes: 5) != 60.0)
        {
            return 9;
        }

        if (!UsageCounterPurge.ShouldPurge(
                "usage:client:Service:svc:Second:20260101100000:Granted",
                count: 1,
                cutoffUtc: rpmFrom.AddMinutes(1)))
        {
            return 10;
        }

        return 0;
    }

    private static long DeriveActiveCount(long running, long granted, long released, long storedActive)
    {
        running = Math.Max(0, running + granted - released);
        return storedActive > 0 ? storedActive : running;
    }
}
