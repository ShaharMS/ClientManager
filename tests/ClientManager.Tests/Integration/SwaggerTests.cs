using System.Net;
using ClientManager.Tests.Helpers;

namespace ClientManager.Tests.Integration;

[Collection(ApiIntegrationCollection.Name)]
public sealed class SwaggerTests(ClientManagerApiFactory factory)
{
    [Fact]
    public async Task Swagger_json_is_available()
    {
        var client = factory.CreateClientWithBaseAddress();

        var response = await client.GetAsync("swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ClientManager API", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swagger_ui_route_is_available()
    {
        var client = factory.CreateClientWithBaseAddress();

        var response = await client.GetAsync("docs/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
