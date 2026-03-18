using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.AdminUI.Services;

public class GlobalRateLimitApiService
{
    private readonly HttpClient _httpClient;

    public GlobalRateLimitApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<GlobalRateLimit>> GetAllAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<GlobalRateLimit>>("api/global-rate-limits") ?? [];
    }

    public async Task<List<GlobalRateLimit>> GetByTargetTypeAsync(GlobalRateLimitTarget targetType)
    {
        return await _httpClient.GetFromJsonAsync<List<GlobalRateLimit>>(
            $"api/global-rate-limits?targetType={targetType}") ?? [];
    }

    public async Task<GlobalRateLimit?> GetByIdAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<GlobalRateLimit>($"api/global-rate-limits/{id}");
    }

    public async Task CreateAsync(GlobalRateLimit limit)
    {
        var response = await _httpClient.PostAsJsonAsync("api/global-rate-limits", limit);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(string id, GlobalRateLimit limit)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/global-rate-limits/{id}", limit);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/global-rate-limits/{id}");
        response.EnsureSuccessStatusCode();
    }
}
