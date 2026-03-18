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
}

public record SystemOverview(
    int TotalClients, int EnabledClients,
    int TotalServices, int EnabledServices,
    int TotalResourcePools, int ActiveAllocations);

public record ResourcePoolStatistics(
    string ResourcePoolId, string Name,
    int MaxSlots, int ActiveAllocations,
    int AvailableSlots, bool HasGlobalRateLimit);
