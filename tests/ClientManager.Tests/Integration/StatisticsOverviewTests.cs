using System.Net.Http.Json;
using ClientManager.Shared.Models.Responses;
using ClientManager.Tests.Helpers;

namespace ClientManager.Tests.Integration;

public sealed class StatisticsOverviewTests
{
    [Fact]
    public async Task Overview_returns_counts_for_seeded_catalog()
    {
        await using var api = new ClientManagerApiFactory();
        var client = api.CreateClientWithBaseAddress();

        await TestCatalogFactory.SeedServiceAsync(client, TestCatalogFactory.CreateService("svc-stats", "Stats Service"));
        await TestCatalogFactory.SeedClientAsync(client, TestCatalogFactory.CreateClient("client-stats", "svc-stats"));

        (await client.GetAsync("api/v2/services/svc-stats")).EnsureSuccessStatusCode();
        (await client.GetAsync("api/v2/clients/client-stats")).EnsureSuccessStatusCode();

        var response = await client.GetAsync("api/v2/statistics/overview");
        response.EnsureSuccessStatusCode();

        var overview = await response.Content.ReadFromJsonAsync<SystemOverviewResponse>();
        Assert.NotNull(overview);
        Assert.True(overview.TotalClients >= 1);
        Assert.True(overview.TotalServices >= 1);
        Assert.True(overview.RequestsPerMinute >= 0);
    }
}
