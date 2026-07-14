using System.Net;
using ClientManager.Tests.Helpers;

namespace ClientManager.Tests.Integration;

public sealed class SeedGatingTests
{
    [Fact]
    public async Task Seed_export_returns_404_when_disabled()
    {
        await using var api = new ClientManagerApiFactory();
        var client = api.CreateClientWithBaseAddress();

        var response = await client.GetAsync("api/v2/seed");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Seed_import_returns_404_when_disabled()
    {
        await using var api = new ClientManagerApiFactory();
        var client = api.CreateClientWithBaseAddress();

        var response = await client.PostAsync("api/v2/seed", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Seed_export_is_available_when_enabled()
    {
        await using var api = new ClientManagerApiFactory().Configure(settings =>
        {
            settings["Seed:SeedApiEnabled"] = "true";
        });
        var client = api.CreateClientWithBaseAddress();

        var response = await client.GetAsync("api/v2/seed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
