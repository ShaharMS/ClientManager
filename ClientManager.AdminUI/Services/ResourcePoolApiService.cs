using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.AdminUI.Services;

public class ResourcePoolApiService
{
    private readonly HttpClient _httpClient;

    public ResourcePoolApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<ResourcePool>> GetAllAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ResourcePool>>("api/resource-pools") ?? [];
    }

    public async Task<ResourcePool?> GetByIdAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<ResourcePool>($"api/resource-pools/{id}");
    }

    public async Task CreateAsync(ResourcePool pool)
    {
        var response = await _httpClient.PostAsJsonAsync("api/resource-pools", pool);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(string id, ResourcePool pool)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/resource-pools/{id}", pool);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/resource-pools/{id}");
        response.EnsureSuccessStatusCode();
    }
}
