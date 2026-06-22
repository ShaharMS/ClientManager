using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Services;

namespace ClientManager.AdminUI.Utils;

public static class ChartSeriesTransform
{
    public static List<ChartPoint> TransformPoints(List<ChartPoint> points, AxisScaleType scale) =>
        scale == AxisScaleType.Logarithmic
            ? points.Select(p => p with
            {
                Value = LogarithmicScaleHelper.Transform(p.Value),
                OriginalValue = p.Value
            }).ToList()
            : points;

    public static List<ClientAreaSeries> GetChartSeries(List<ClientAreaSeries> series, AxisScaleType scale) =>
        scale == AxisScaleType.Logarithmic
            ? TransformStackedLogarithmic(series)
            : series;

    public static List<ClientAreaSeries> TransformStackedLogarithmic(List<ClientAreaSeries> series)
    {
        if (series.Count == 0)
        {
            return series;
        }

        var totals = new Dictionary<string, double>();
        foreach (var s in series)
        {
            foreach (var p in s.Points)
            {
                totals[p.Label] = totals.GetValueOrDefault(p.Label) + p.Value;
            }
        }

        return series.Select(s => new ClientAreaSeries(s.ClientId, s.ClientName,
            s.Points.Select(p =>
            {
                var total = totals.GetValueOrDefault(p.Label);
                if (total <= 0)
                {
                    return p with { Value = 0, OriginalValue = p.Value };
                }

                var logTotal = LogarithmicScaleHelper.Transform(total);
                return p with { Value = (p.Value / total) * logTotal, OriginalValue = p.Value };
            }).ToList(),
            s.Hidden
        )).ToList();
    }
}
