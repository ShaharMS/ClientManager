namespace ClientManager.AdminUI.Services;

/// <summary>
/// Aggregates chart series data to keep visualizations readable when
/// there are many entities.
/// </summary>
public static class ChartAggregator
{
    public const int DefaultTopN = 10;
    public const string OthersId = "__others__";
    public const string OthersLabel = "Others";
    public const string AggregateSeriesId = "__aggregate__";
    public const string DeniedSeriesIdSuffix = "|denied";
    public const string DeniedUnauthSuffix = "|denied|unauth";
    public const string DeniedBlockedSuffix = "|denied|blocked";
    public const string DeniedRateLimitedSuffix = "|denied|ratelimited";
    public const string DeniedCapacitySuffix = "|denied|capacity";

    public record AggregatedPoint(string Label, double Value);
    public record AggregatedSeries(string Id, string Name, List<AggregatedPoint> Points);

    /// <summary>
    /// Given a list of series, returns at most topN individual series plus
    /// one "Others" series aggregating the rest. Series are ranked by total
    /// value (sum of all points).
    /// </summary>
    public static List<AggregatedSeries> Aggregate(
        List<AggregatedSeries> series,
        int topN = DefaultTopN)
    {
        if (series.Count <= topN)
            return series;

        var ranked = series
            .OrderByDescending(s => s.Points.Sum(p => p.Value))
            .ToList();

        var top = ranked.Take(topN).ToList();
        var rest = ranked.Skip(topN).ToList();

        var allLabels = rest
            .SelectMany(s => s.Points.Select(p => p.Label))
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        var othersPoints = allLabels.Select(label => new AggregatedPoint(
            label,
            rest.Sum(s => s.Points.FirstOrDefault(p => p.Label == label)?.Value ?? 0)
        )).ToList();

        top.Add(new AggregatedSeries(OthersId, OthersLabel, othersPoints));
        return top;
    }
}
