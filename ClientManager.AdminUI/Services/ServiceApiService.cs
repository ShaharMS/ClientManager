using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;

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

        _cachedAll = await _httpClient.GetFromJsonAsync<List<Service>>("api/services") ?? [];
        _cachedAllAt = DateTime.UtcNow;
        return _cachedAll;
    }

    public async Task<Service?> GetByIdAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<Service>($"api/services/{id}");
    }

    public async Task CreateAsync(Service service)
    {
        var response = await _httpClient.PostAsJsonAsync("api/services", service);
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }

    public async Task UpdateAsync(string id, Service service)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/services/{id}", service);
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/services/{id}");
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }
}
