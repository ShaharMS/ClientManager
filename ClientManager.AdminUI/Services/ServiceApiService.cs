using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services;

public class ServiceApiService
{
    private readonly HttpClient _httpClient;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private List<Service>? _cachedAll;
    private DateTime _cachedAllAt;

    public ServiceApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<Service>> GetAllAsync()
    {
        if (_cachedAll is not null && DateTime.UtcNow - _cachedAllAt < CacheTtl)
            return _cachedAll;

        var response = await _httpClient.GetFromJsonAsync<PagedResponse<Service>>(
            "api/v1/services?pageSize=100");
        _cachedAll = response?.Items?.ToList() ?? [];
        _cachedAllAt = DateTime.UtcNow;
        return _cachedAll;
    }

    public async Task<Service?> GetByIdAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<Service>($"api/v1/services/{id}");
    }

    public async Task CreateAsync(Service service)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/services", service);
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }

    public async Task UpdateAsync(string id, Service service)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/services/{id}", service);
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/services/{id}");
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }
}
