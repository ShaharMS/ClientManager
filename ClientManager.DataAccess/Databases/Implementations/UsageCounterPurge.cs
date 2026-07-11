namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Purge rules for pending <c>usage:</c> counter keys.
/// </summary>
public static class UsageCounterPurge
{
    public static bool ShouldPurge(string key, long count, DateTime cutoffUtc, DateTime? windowStart = null)
    {
        if (count <= 0)
        {
            return true;
        }

        if (UsageSegmentHelper.TryParseUsageCounterKey(
                key,
                out _,
                out _,
                out _,
                out var secondTimestamp,
                out _,
                out _))
        {
            return secondTimestamp < cutoffUtc;
        }

        return windowStart is not null && windowStart.Value < cutoffUtc;
    }

    internal static IReadOnlyDictionary<string, long> FilterLiveCounters(
        IReadOnlyDictionary<string, long> counters,
        DateTime cutoffUtc)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (key, value) in counters)
        {
            if (value <= 0 ||
                !UsageSegmentHelper.TryParseUsageCounterKey(
                    key,
                    out _,
                    out _,
                    out _,
                    out var secondTimestamp,
                    out _,
                    out _))
            {
                continue;
            }

            if (secondTimestamp >= cutoffUtc)
            {
                result[key] = value;
            }
        }

        return result;
    }
}
