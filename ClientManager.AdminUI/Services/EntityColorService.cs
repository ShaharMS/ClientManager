using System.Security.Cryptography;
using System.Text;

namespace ClientManager.AdminUI.Services;

/// <summary>
/// Generates deterministic colors for entity IDs using a stable hash.
/// The same ID always produces the same color across sessions and pages.
/// </summary>
public class EntityColorService
{
    private static readonly string[] Palette =
    [
        "#6366f1", "#f59e0b", "#22c55e", "#ef4444", "#3b82f6",
        "#ec4899", "#14b8a6", "#f97316", "#8b5cf6", "#06b6d4",
        "#84cc16", "#e11d48", "#0ea5e9", "#d946ef", "#10b981",
        "#facc15", "#7c3aed", "#fb923c", "#2dd4bf", "#a855f7"
    ];

    /// <summary>
    /// Returns a deterministic color string for the given entity ID.
    /// The "__others__" aggregate always returns neutral slate gray.
    /// </summary>
    public string GetColor(string entityId)
    {
        if (entityId == ChartAggregator.OthersId)
            return "#94a3b8";

        if (entityId == ChartAggregator.AggregateSeriesId)
            return "#8b5cf6";

        var hash = GetStableHash(entityId);
        var index = (int)(hash % (uint)Palette.Length);
        return Palette[index];
    }

    public string GetSeriesColor(string seriesId)
    {
        if (seriesId.EndsWith(ChartAggregator.DeniedUnauthSuffix, StringComparison.Ordinal))
        {
            return "rgba(239, 68, 68, 0.45)";
        }

        if (seriesId.EndsWith(ChartAggregator.DeniedBlockedSuffix, StringComparison.Ordinal))
        {
            return "rgba(107, 114, 128, 0.45)";
        }

        if (seriesId.EndsWith(ChartAggregator.DeniedRateLimitedSuffix, StringComparison.Ordinal))
        {
            return "rgba(234, 179, 8, 0.45)";
        }

        if (seriesId.EndsWith(ChartAggregator.DeniedCapacitySuffix, StringComparison.Ordinal))
        {
            return "rgba(249, 115, 22, 0.45)";
        }

        if (seriesId.EndsWith(ChartAggregator.OffBudgetSuffix, StringComparison.Ordinal))
        {
            return "rgba(148, 163, 184, 0.45)";
        }

        if (seriesId.EndsWith(ChartAggregator.DeniedSeriesIdSuffix, StringComparison.Ordinal))
        {
            var baseId = seriesId[..^ChartAggregator.DeniedSeriesIdSuffix.Length];
            return ToTransparent(GetColor(baseId), 0.45);
        }

        return GetColor(seriesId);
    }

    /// <summary>
    /// Returns a deterministic color for the entity at the given position
    /// in an ordered list. Uses palette first, then falls back to hash.
    /// </summary>
    public string GetColorByIndex(string entityId, int index)
    {
        if (index < Palette.Length)
            return Palette[index];
        return GetColor(entityId);
    }

    /// <summary>
    /// Returns colors for a batch of entity IDs, maintaining order.
    /// </summary>
    public Dictionary<string, string> GetColors(IEnumerable<string> entityIds)
    {
        var result = new Dictionary<string, string>();
        var idx = 0;
        foreach (var id in entityIds)
        {
            if (!result.ContainsKey(id))
            {
                result[id] = GetColorByIndex(id, idx);
                idx++;
            }
        }
        return result;
    }

    private static uint GetStableHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static string ToTransparent(string hex, double alpha)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
        {
            return hex;
        }

        var r = Convert.ToInt32(hex[..2], 16);
        var g = Convert.ToInt32(hex[2..4], 16);
        var b = Convert.ToInt32(hex[4..6], 16);
        return $"rgba({r}, {g}, {b}, {alpha.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
    }
}
