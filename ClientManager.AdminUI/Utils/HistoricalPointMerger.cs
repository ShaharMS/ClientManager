using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Utils;

internal static class HistoricalPointMerger
{
    internal static List<HistoricalUsagePoint> SumByTimestamp(
        IEnumerable<IReadOnlyList<HistoricalUsagePoint>> targetPoints)
    {
        var merged = new SortedDictionary<DateTime, (long Granted, long Denied, long Unauth, long Blocked, long Rate, long Capacity, long Released, long Active)>();

        foreach (var points in targetPoints)
        {
            foreach (var point in points)
            {
                if (!merged.TryGetValue(point.Timestamp, out var totals))
                {
                    totals = default;
                }

                merged[point.Timestamp] = (
                    totals.Granted + point.GrantedCount,
                    totals.Denied + point.DeniedCount,
                    totals.Unauth + point.DeniedUnauthenticatedCount,
                    totals.Blocked + point.DeniedBlockedCount,
                    totals.Rate + point.DeniedRateLimitedCount,
                    totals.Capacity + point.DeniedCapacityLimitedCount,
                    totals.Released + point.ReleasedCount,
                    totals.Active + point.ActiveCount);
            }
        }

        return merged
            .Select(kvp => new HistoricalUsagePoint(
                kvp.Key,
                kvp.Value.Granted,
                kvp.Value.Denied,
                kvp.Value.Unauth,
                kvp.Value.Blocked,
                kvp.Value.Rate,
                kvp.Value.Capacity,
                kvp.Value.Released,
                kvp.Value.Active))
            .ToList();
    }
}
