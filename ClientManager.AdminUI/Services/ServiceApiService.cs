using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.AdminUI.Services;

public class ServiceApiService
{
    private readonly HttpClient _httpClient;

    public ServiceApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<Service>> GetAllAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<Service>>("api/services") ?? [];
    }

    public async Task<Service?> GetByIdAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<Service>($"api/services/{id}");
    }

    public async Task CreateAsync(Service service)
    {
        var response = await _httpClient.PostAsJsonAsync("api/services", service);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(string id, Service service)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/services/{id}", service);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/services/{id}");
        response.EnsureSuccessStatusCode();
    }
}
