using System.Net.Http.Json;

namespace ClientManager.AdminUI.Services;

public class StatisticsApiService
{
    private readonly HttpClient _httpClient;

    public StatisticsApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<SystemOverview?> GetOverviewAsync()
    {
        return await _httpClient.GetFromJsonAsync<SystemOverview>("api/statistics/overview");
    }

    public async Task<List<ResourcePoolStatistics>> GetResourcePoolStatsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ResourcePoolStatistics>>(
            "api/statistics/resource-pools") ?? [];
    }

    public async Task<GlobalUsageStats?> GetGlobalUsageStatsAsync()
    {
        return await _httpClient.GetFromJsonAsync<GlobalUsageStats>("api/statistics/global-usage");
    }

    public async Task<List<TargetUsageTimeSeries>> GetUsageTimeSeriesAsync(
        string filterType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, string? granularity = null)
    {
        var url = $"api/statistics/usage-timeseries?filterType={Uri.EscapeDataString(filterType)}&targetIds={Uri.EscapeDataString(string.Join(",", targetIds))}";
        if (clientIds?.Any() == true)
        {
            url += $"&clientIds={Uri.EscapeDataString(string.Join(",", clientIds))}";
        }
        if (from is not null)
            url += $"&from={from.Value:O}";
        if (to is not null)
            url += $"&to={to.Value:O}";
        if (granularity is not null)
            url += $"&granularity={Uri.EscapeDataString(granularity)}";

        return await _httpClient.GetFromJsonAsync<List<TargetUsageTimeSeries>>(url) ?? [];
    }

    public async Task<List<TargetClientUsageBreakdown>> GetClientUsageBreakdownAsync(
        string filterType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, string? granularity = null)
    {
        var url = $"api/statistics/client-usage-breakdown?filterType={Uri.EscapeDataString(filterType)}&targetIds={Uri.EscapeDataString(string.Join(",", targetIds))}";
        if (clientIds?.Any() == true)
        {
            url += $"&clientIds={Uri.EscapeDataString(string.Join(",", clientIds))}";
        }
        if (from is not null)
            url += $"&from={from.Value:O}";
        if (to is not null)
            url += $"&to={to.Value:O}";
        if (granularity is not null)
            url += $"&granularity={Uri.EscapeDataString(granularity)}";

        return await _httpClient.GetFromJsonAsync<List<TargetClientUsageBreakdown>>(url) ?? [];
    }

    public async Task<ClientSummaries?> GetClientSummariesAsync()
    {
        return await _httpClient.GetFromJsonAsync<ClientSummaries>("api/statistics/client-summaries");
    }

    public async Task<List<HistoricalUsageData>> GetHistoricalUsageAsync(
        string filterType, IEnumerable<string> targetIds, string? clientId,
        DateTime from, DateTime to, string granularity)
    {
        var url = $"api/statistics/historical-usage?filterType={Uri.EscapeDataString(filterType)}"
            + $"&targetIds={Uri.EscapeDataString(string.Join(",", targetIds))}"
            + $"&from={from:O}&to={to:O}"
            + $"&granularity={Uri.EscapeDataString(granularity)}";
        if (clientId is not null)
        {
            url += $"&clientId={Uri.EscapeDataString(clientId)}";
        }
        return await _httpClient.GetFromJsonAsync<List<HistoricalUsageData>>(url) ?? [];
    }
}

public record SystemOverview(
    int TotalClients, int EnabledClients,
    int TotalServices, int EnabledServices,
    int TotalResourcePools, int ActiveAllocations);

public record ResourcePoolStatistics(
    string ResourcePoolId, string Name,
    int MaxSlots, int ActiveAllocations,
    int AvailableSlots, bool HasGlobalRateLimit);

public record GlobalUsageStats(
    double RequestsPerMinute, int TotalPoolSlots,
    int AcquiredPoolSlots, double AcquisitionPercentage);

public record TargetUsageTimeSeries(
    string TargetId,
    List<UsageTimeSeriesPoint> UsagePoints,
    List<UsageTimeSeriesPoint> CapPoints);

public record UsageTimeSeriesPoint(DateTime Timestamp, double Value);

public record TargetClientUsageBreakdown(string TargetId, List<ClientUsageItem> Entries);

public record ClientUsageItem(string ClientId, string ClientName, double Value);

public record ClientSummaries(List<ClientSummaryItem> Rows);

public record ClientSummaryItem(
    string ClientId, string DisplayName,
    int AccessibleServices, string TotalRateLimitCap,
    int AccessiblePools, int UsedSlots, int TotalAccessibleSlots);

public record HistoricalUsageData(
    string TargetId, string TargetType, string Granularity,
    List<HistoricalUsagePoint> Points);

public record HistoricalUsagePoint(
    DateTime Timestamp, long GrantedCount, long DeniedCount,
    long ReleasedCount, long ActiveCount);
