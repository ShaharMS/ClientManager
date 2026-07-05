using ClientManager.AdminUI.Models;
using Radzen;

namespace ClientManager.AdminUI.Utils;

public static class QueryParamParsers
{
    public const string Range = "range";
    public const string From = "from";
    public const string To = "to";
    public const string Scale = "scale";
    public const string Poll = "poll";
    public const string Clients = "clients";
    public const string Search = "search";
    public const string Enabled = "enabled";
    public const string Page = "page";
    public const string Sort = "sort";
    public const string Dir = "dir";
    public const string Type = "type";
    public const string Target = "target";
    public const string Service = "service";
    public const string Pool = "pool";
    public const string Metric = "metric";

    public static ChartTimeRange? TryParseTimeRange(string? range, string? from, string? to)
    {
        if (string.IsNullOrWhiteSpace(range))
        {
            return null;
        }

        if (string.Equals(range, "custom", StringComparison.OrdinalIgnoreCase))
        {
            if (DateTime.TryParse(from, null, System.Globalization.DateTimeStyles.RoundtripKind, out var fromUtc)
                && DateTime.TryParse(to, null, System.Globalization.DateTimeStyles.RoundtripKind, out var toUtc))
            {
                return ChartTimeRange.FromCustom(
                    fromUtc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc) : fromUtc.ToUniversalTime(),
                    toUtc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(toUtc, DateTimeKind.Utc) : toUtc.ToUniversalTime());
            }

            return null;
        }

        var preset = TimeRangePreset.FindByKey(range);
        return preset is not null ? ChartTimeRange.FromPreset(preset) : null;
    }

    public static void WriteTimeRange(IDictionary<string, string?> query, ChartTimeRange range, ChartTimeRange defaultRange)
    {
        if (RangesEqual(range, defaultRange))
        {
            return;
        }

        if (range.Mode == ChartTimeRangeMode.Custom)
        {
            query[Range] = "custom";
            query[From] = range.CustomFromUtc.ToString("O");
            query[To] = range.CustomToUtc.ToString("O");
            return;
        }

        var preset = TimeRangePreset.All.FirstOrDefault(p => p.Duration == range.RelativeDuration);
        if (preset is not null)
        {
            query[Range] = preset.Key;
        }
    }

    public static string? TryGetRangeKey(ChartTimeRange range)
    {
        if (range.Mode == ChartTimeRangeMode.Custom)
        {
            return "custom";
        }

        return TimeRangePreset.All.FirstOrDefault(p => p.Duration == range.RelativeDuration)?.Key;
    }

    public static AxisScaleType? TryParseScale(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "linear" => AxisScaleType.Linear,
            "log" => AxisScaleType.Logarithmic,
            _ => null
        };

    public static string? FormatScale(AxisScaleType scale, AxisScaleType defaultScale) =>
        scale == defaultScale ? null : scale == AxisScaleType.Logarithmic ? "log" : "linear";

    public static PollingIntervalPreset? TryParsePoll(string? value) =>
        PollingIntervalPreset.FindByKey(value);

    public static string? FormatPoll(string? key, string defaultKey) =>
        string.IsNullOrEmpty(key) || string.Equals(key, defaultKey, StringComparison.OrdinalIgnoreCase) ? null : key;

    public static IEnumerable<string>? ParseClientIds(string? csv, IReadOnlySet<string>? validIds)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return null;
        }

        var ids = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (validIds is null)
        {
            return ids.Length > 0 ? ids : null;
        }

        var filtered = ids.Where(validIds.Contains).ToList();
        return filtered.Count > 0 ? filtered : null;
    }

    public static string? FormatClientIds(IEnumerable<string>? ids)
    {
        var list = ids?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        return list is { Count: > 0 } ? string.Join(',', list) : null;
    }

    public static bool? TryParseEnabled(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ => null
        };

    public static string? FormatEnabled(bool? enabled) =>
        enabled switch
        {
            true => "true",
            false => "false",
            _ => null
        };

    public static int TryParsePage(string? value, int defaultPage = 1)
    {
        if (int.TryParse(value, out var page) && page > 0)
        {
            return page;
        }

        return defaultPage;
    }

    public static string? FormatPage(int page) => page <= 1 ? null : page.ToString();

    public static (string? Property, SortOrder? Order) TryParseSort(string? sort, string? dir)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return (null, null);
        }

        var order = dir?.ToLowerInvariant() switch
        {
            "desc" => SortOrder.Descending,
            "asc" => SortOrder.Ascending,
            _ => SortOrder.Ascending
        };

        return (sort, order);
    }

    public static void WriteSort(IDictionary<string, string?> query, string? property, SortOrder? order)
    {
        if (string.IsNullOrWhiteSpace(property) || order is null)
        {
            return;
        }

        query[Sort] = property;
        query[Dir] = order == SortOrder.Descending ? "desc" : "asc";
    }

    public static DataGridSettings ApplyGridSettings(DataGridSettings current, int page, string? sortProperty, SortOrder? sortOrder)
    {
        var columns = current.Columns?.Select(c => new DataGridColumnSettings
        {
            UniqueID = c.UniqueID,
            Property = c.Property,
            Visible = c.Visible,
            Width = c.Width,
            OrderIndex = c.OrderIndex,
            SortOrder = null,
            SortIndex = c.SortIndex
        }).ToList() ?? [];

        var settings = new DataGridSettings
        {
            CurrentPage = Math.Max(0, page - 1),
            PageSize = current.PageSize,
            Columns = columns
        };

        if (string.IsNullOrWhiteSpace(sortProperty) || sortOrder is null)
        {
            return settings;
        }

        var column = columns.FirstOrDefault(c =>
            string.Equals(c.Property, sortProperty, StringComparison.OrdinalIgnoreCase));

        if (column is not null)
        {
            column.SortOrder = sortOrder;
        }
        else
        {
            columns.Add(new DataGridColumnSettings
            {
                Property = sortProperty,
                SortOrder = sortOrder
            });
        }

        settings.Columns = columns;
        return settings;
    }

    public static (string? Property, SortOrder? Order) ReadGridSort(DataGridSettings? settings)
    {
        var sorted = settings?.Columns?
            .Where(c => c.SortOrder is not null)
            .OrderBy(c => c.SortIndex ?? 0)
            .FirstOrDefault();

        return sorted is null ? (null, null) : (sorted.Property, sorted.SortOrder);
    }

    public static int ReadGridPage(DataGridSettings? settings) =>
        (settings?.CurrentPage ?? 0) + 1;

    public static int ClampPage(int page, int itemCount, int pageSize)
    {
        if (itemCount <= 0)
        {
            return 1;
        }

        var maxPage = Math.Max(1, (int)Math.Ceiling(itemCount / (double)Math.Max(1, pageSize)));
        return Math.Clamp(page, 1, maxPage);
    }

    public static bool RangesEqual(ChartTimeRange a, ChartTimeRange b)
    {
        if (a.Mode != b.Mode)
        {
            return false;
        }

        return a.Mode == ChartTimeRangeMode.Custom
            ? a.CustomFromUtc == b.CustomFromUtc && a.CustomToUtc == b.CustomToUtc
            : a.RelativeDuration == b.RelativeDuration;
    }

    public static void WriteIfPresent(IDictionary<string, string?> query, string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        query[key] = value;
    }

    // ponytail: smallest runnable check if parse/format regress
    public static void SelfCheck()
    {
        if (TryParseTimeRange("1h", null, null) is null)
        {
            throw new InvalidOperationException("range parse failed");
        }

        if (TryParsePage("2") != 2 || FormatPage(1) is not null)
        {
            throw new InvalidOperationException("page parse/format failed");
        }

        var clients = ParseClientIds("a,b", new HashSet<string> { "a", "b", "c" })?.ToList();
        if (clients is null || clients.Count != 2 || FormatClientIds(clients) != "a,b")
        {
            throw new InvalidOperationException("clients parse/format failed");
        }
    }
}
