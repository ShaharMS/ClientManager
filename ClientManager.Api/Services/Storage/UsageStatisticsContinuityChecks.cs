namespace ClientManager.Api.Services.Storage;

using ClientManager.Shared.Models.Entities;

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

        return 0;
    }

    private static long DeriveActiveCount(long running, long granted, long released, long storedActive)
    {
        running = Math.Max(0, running + granted - released);
        return storedActive > 0 ? storedActive : running;
    }
}
