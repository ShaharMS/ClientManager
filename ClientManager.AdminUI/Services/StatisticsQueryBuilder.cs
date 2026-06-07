namespace ClientManager.AdminUI.Services;

/// <summary>
/// Builds statistics API query strings with shared encoding rules.
/// </summary>
internal static class StatisticsQueryBuilder
{
    public static string BuildTargetQuery(
        string path,
        string filterType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds = null,
        DateTime? from = null,
        DateTime? to = null,
        string? granularity = null,
        string? singleClientId = null)
    {
        var targetIdList = targetIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (targetIdList.Count == 0)
        {
            return string.Empty;
        }

        var url = $"{path}?filterType={Uri.EscapeDataString(filterType)}"
            + $"&targetIds={Uri.EscapeDataString(string.Join(",", targetIdList))}";

        if (clientIds?.Any() == true)
        {
            url += $"&clientIds={Uri.EscapeDataString(string.Join(",", clientIds))}";
        }

        if (from is not null)
        {
            url += $"&from={from.Value:O}";
        }

        if (to is not null)
        {
            url += $"&to={to.Value:O}";
        }

        if (granularity is not null)
        {
            url += $"&granularity={Uri.EscapeDataString(granularity)}";
        }

        if (singleClientId is not null)
        {
            url += $"&clientId={Uri.EscapeDataString(singleClientId)}";
        }

        return url;
    }
}
