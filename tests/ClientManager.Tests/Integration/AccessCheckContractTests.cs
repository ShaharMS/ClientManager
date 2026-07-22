using System.Net;
using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Http;
using ClientManager.Shared.Models.Enums;
using ClientManager.Tests.Helpers;

namespace ClientManager.Tests.Integration;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AccessCheckContractTests(ClientManagerApiFactory factory)
{
    private const string ServiceId = "svc-access";
    private const string ClientId = "client-access";

    [Fact]
    public async Task Granted_access_returns_200()
    {
        var client = factory.CreateClientWithBaseAddress();
        await SeedAsync(client);

        var response = await client.GetAsync($"api/v2/access/check?clientId={ClientId}&serviceId={ServiceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Granted_access_without_rate_limit_omits_remaining_requests()
    {
        var client = factory.CreateClientWithBaseAddress();
        await SeedAsync(client);

        var response = await client.GetAsync($"api/v2/access/check?clientId={ClientId}&serviceId={ServiceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccessCheckResponse>();
        Assert.NotNull(body);
        Assert.Null(body.RemainingRequests);
    }

    [Fact]
    public async Task Granted_access_with_rate_limit_returns_remaining_requests()
    {
        const string clientId = "client-access-rate-remaining";
        const string serviceId = "svc-access-rate-remaining";
        var client = factory.CreateClientWithBaseAddress();
        await SeedAsync(client, clientId, serviceId, new RateLimitPolicy
        {
            Strategy = RateLimitStrategy.FixedWindow,
            MaxRequests = 5,
            Window = TimeSpan.FromMinutes(1)
        });

        var response = await client.GetAsync($"api/v2/access/check?clientId={clientId}&serviceId={serviceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccessCheckResponse>();
        Assert.NotNull(body);
        Assert.Equal(4, body.RemainingRequests);
    }

    [Fact]
    public async Task Unknown_client_returns_400_with_problem_headers()
    {
        var client = factory.CreateClientWithBaseAddress();
        await TestCatalogFactory.SeedServiceAsync(client, TestCatalogFactory.CreateService(ServiceId));

        var response = await client.GetAsync($"api/v2/access/check?clientId=missing&serviceId={ServiceId}");

        await ProblemResponseAssertions.AssertProblemAsync(response, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Missing_service_mapping_returns_401_with_problem_headers()
    {
        var client = factory.CreateClientWithBaseAddress();
        await TestCatalogFactory.SeedServiceAsync(client, TestCatalogFactory.CreateService(ServiceId));
        await TestCatalogFactory.SeedClientAsync(client, new ClientConfiguration
        {
            Id = ClientId,
            Name = ClientId,
            Services = []
        });

        var response = await client.GetAsync($"api/v2/access/check?clientId={ClientId}&serviceId={ServiceId}");

        await ProblemResponseAssertions.AssertProblemAsync(response, StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Disabled_client_returns_403_with_problem_headers()
    {
        var client = factory.CreateClientWithBaseAddress();
        await TestCatalogFactory.SeedServiceAsync(client, TestCatalogFactory.CreateService(ServiceId));
        await TestCatalogFactory.SeedClientAsync(client, TestCatalogFactory.CreateClient(ClientId, ServiceId, isEnabled: false));

        var response = await client.GetAsync($"api/v2/access/check?clientId={ClientId}&serviceId={ServiceId}");

        await ProblemResponseAssertions.AssertProblemAsync(response, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Unknown_service_returns_404_with_problem_headers()
    {
        var client = factory.CreateClientWithBaseAddress();
        await TestCatalogFactory.SeedClientAsync(client, TestCatalogFactory.CreateClient(ClientId, ServiceId));

        var response = await client.GetAsync($"api/v2/access/check?clientId={ClientId}&serviceId=missing-service");

        await ProblemResponseAssertions.AssertProblemAsync(response, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Rate_limited_client_returns_429_with_retry_after_and_problem_headers()
    {
        const string clientId = "client-access-rate-429";
        const string serviceId = "svc-access-rate-429";
        var client = factory.CreateClientWithBaseAddress();
        await SeedAsync(client, clientId, serviceId, new RateLimitPolicy
        {
            Strategy = RateLimitStrategy.FixedWindow,
            MaxRequests = 1,
            Window = TimeSpan.FromMinutes(1)
        });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"api/v2/access/check?clientId={clientId}&serviceId={serviceId}")).StatusCode);

        var response = await client.GetAsync($"api/v2/access/check?clientId={clientId}&serviceId={serviceId}");

        await ProblemResponseAssertions.AssertProblemAsync(response, StatusCodes.Status429TooManyRequests, expectRetryAfter: true);
    }

    private static async Task SeedAsync(
        HttpClient client,
        string clientId = ClientId,
        string serviceId = ServiceId,
        RateLimitPolicy? rateLimit = null)
    {
        await TestCatalogFactory.SeedServiceAsync(client, TestCatalogFactory.CreateService(serviceId));
        await TestCatalogFactory.SeedClientAsync(client, TestCatalogFactory.CreateClient(clientId, serviceId, rateLimit: rateLimit));
    }
}
