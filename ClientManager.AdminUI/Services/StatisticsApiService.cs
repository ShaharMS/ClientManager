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

    public async Task<UsageTimeSeries?> GetUsageTimeSeriesAsync(
        string filterType, string targetId, IEnumerable<string>? clientIds)
    {
        var url = $"api/statistics/usage-timeseries?filterType={Uri.EscapeDataString(filterType)}&targetId={Uri.EscapeDataString(targetId)}";
        if (clientIds?.Any() == true)
        {
            url += $"&clientIds={Uri.EscapeDataString(string.Join(",", clientIds))}";
        }
        return await _httpClient.GetFromJsonAsync<UsageTimeSeries>(url);
    }

    public async Task<ClientUsageBreakdown?> GetClientUsageBreakdownAsync(
        string filterType, string targetId, IEnumerable<string>? clientIds)
    {
        var url = $"api/statistics/client-usage-breakdown?filterType={Uri.EscapeDataString(filterType)}&targetId={Uri.EscapeDataString(targetId)}";
        if (clientIds?.Any() == true)
        {
            url += $"&clientIds={Uri.EscapeDataString(string.Join(",", clientIds))}";
        }
        return await _httpClient.GetFromJsonAsync<ClientUsageBreakdown>(url);
    }

    public async Task<ClientSummaries?> GetClientSummariesAsync()
    {
        return await _httpClient.GetFromJsonAsync<ClientSummaries>("api/statistics/client-summaries");
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

public record UsageTimeSeries(
    List<UsageTimeSeriesPoint> UsagePoints,
    List<UsageTimeSeriesPoint> CapPoints);

public record UsageTimeSeriesPoint(DateTime Timestamp, double Value);

public record ClientUsageBreakdown(List<ClientUsageItem> Entries);

public record ClientUsageItem(string ClientId, string ClientName, double Value);

public record ClientSummaries(List<ClientSummaryItem> Rows);

public record ClientSummaryItem(
    string ClientId, string DisplayName,
    int AccessibleServices, string TotalRateLimitCap,
    int AccessiblePools, int UsedSlots, int TotalAccessibleSlots);
