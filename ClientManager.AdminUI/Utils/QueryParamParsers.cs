using Radzen;

namespace ClientManager.AdminUI.Utils;

public static class QueryParamParsers
{
    public const string Search = "search";
    public const string Enabled = "enabled";
    public const string Page = "page";
    public const string Sort = "sort";
    public const string Dir = "dir";

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
        if (TryParsePage("2") != 2 || FormatPage(1) is not null)
        {
            throw new InvalidOperationException("page parse/format failed");
        }

        if (TryParseEnabled("true") != true || FormatEnabled(null) is not null)
        {
            throw new InvalidOperationException("enabled parse/format failed");
        }
    }
}
