using System.Net.Http.Json;
using System.Text.Json;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Tests.Helpers;

public static class TestCatalogFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Service CreateService(string id = "svc-test", string name = "Test Service") =>
        new() { Id = id, Name = name, IsEnabled = true };

    public static ClientConfiguration CreateClient(
        string id,
        string serviceId,
        bool isEnabled = true,
        bool isAllowed = true,
        RateLimitPolicy? rateLimit = null) =>
        new()
        {
            Id = id,
            Name = id,
            IsEnabled = isEnabled,
            Services = new Dictionary<string, ServiceAccessSettings>
            {
                [serviceId] = new()
                {
                    IsAllowed = isAllowed,
                    RateLimit = rateLimit
                }
            }
        };

    public static async Task SeedServiceAsync(HttpClient client, Service service)
    {
        var response = await client.PostAsJsonAsync("api/v1/services", service, JsonOptions);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            response = await client.PutAsJsonAsync($"api/v1/services/{service.Id}", service, JsonOptions);
        }

        response.EnsureSuccessStatusCode();
    }

    public static async Task SeedClientAsync(HttpClient client, ClientConfiguration configuration)
    {
        var response = await client.PostAsJsonAsync("api/v1/clients", configuration, JsonOptions);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            response = await client.PutAsJsonAsync($"api/v1/clients/{configuration.Id}", configuration, JsonOptions);
        }

        response.EnsureSuccessStatusCode();
    }
}
