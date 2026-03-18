using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.AdminUI.Services;

public class ClientApiService
{
    private readonly HttpClient _httpClient;

    public ClientApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<ClientConfiguration>> GetAllAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ClientConfiguration>>("api/clients") ?? [];
    }

    public async Task<ClientConfiguration?> GetByIdAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<ClientConfiguration>($"api/clients/{id}");
    }

    public async Task CreateAsync(ClientConfiguration config)
    {
        var response = await _httpClient.PostAsJsonAsync("api/clients", config);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(string id, ClientConfiguration config)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{id}", config);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/clients/{id}");
        response.EnsureSuccessStatusCode();
    }
}
