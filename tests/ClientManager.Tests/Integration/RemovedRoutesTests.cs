using System.Net;
using ClientManager.Tests.Helpers;

namespace ClientManager.Tests.Integration;

[Collection(ApiIntegrationCollection.Name)]
public sealed class RemovedRoutesTests(ClientManagerApiFactory factory)
{
    [Theory]
    [InlineData("/api/v1/does-not-exist")]
    [InlineData("/api/v1/clients/client-a/services/svc-a")]
    [InlineData("/api/v1/resource-pools")]
    [InlineData("/api/v1/metrics/prometheus")]
    public async Task Removed_or_unknown_routes_return_404(string path)
    {
        var client = factory.CreateClientWithBaseAddress();

        var response = await client.GetAsync(path.TrimStart('/'));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
