using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
using ClientManager.Tests.Helpers;

namespace ClientManager.Tests.Integration;

[Collection(ApiIntegrationCollection.Name)]
public sealed class CatalogCrudTests(ClientManagerApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Service_crud_roundtrip_works()
    {
        var client = factory.CreateClientWithBaseAddress();
        var service = TestCatalogFactory.CreateService("svc-crud", "CRUD Service");

        var create = await client.PostAsJsonAsync("api/v1/services", service, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var get = await client.GetAsync("api/v1/services/svc-crud");
        get.EnsureSuccessStatusCode();
        var fetched = await get.Content.ReadFromJsonAsync<Service>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal("svc-crud", fetched.Id);

        service = service with { Name = "Updated Service" };
        var update = await client.PutAsJsonAsync("api/v1/services/svc-crud", service, JsonOptions);
        update.EnsureSuccessStatusCode();

        var search = await client.PostAsJsonAsync("api/v1/services/search", DocumentQuery.All, JsonOptions);
        search.EnsureSuccessStatusCode();
        var results = await search.Content.ReadFromJsonAsync<PagedResponse<Service>>(JsonOptions);
        Assert.NotNull(results);
        Assert.Contains(results.Items, item => item.Id == "svc-crud" && item.Name == "Updated Service");

        var delete = await client.DeleteAsync("api/v1/services/svc-crud");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var missing = await client.GetAsync("api/v1/services/svc-crud");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Client_crud_roundtrip_works()
    {
        var client = factory.CreateClientWithBaseAddress();
        var configuration = new ClientConfiguration
        {
            Id = "client-crud",
            Name = "CRUD Client",
            Services = []
        };

        var create = await client.PostAsJsonAsync("api/v1/clients", configuration, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var get = await client.GetAsync("api/v1/clients/client-crud");
        get.EnsureSuccessStatusCode();

        configuration = configuration with { Name = "Updated Client" };
        var update = await client.PutAsJsonAsync("api/v1/clients/client-crud", configuration, JsonOptions);
        update.EnsureSuccessStatusCode();

        var delete = await client.DeleteAsync("api/v1/clients/client-crud");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }
}
