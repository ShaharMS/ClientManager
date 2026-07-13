using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ClientManager.Tests.Helpers;

using ApiProgram = ClientManager.Api.Program;

/// <summary>
/// Boots the API with isolated Redis key prefixes for integration tests.
/// </summary>
public sealed class ClientManagerApiFactory : WebApplicationFactory<ApiProgram>, IAsyncLifetime
{
    private readonly ConcurrentDictionary<string, string?> _settings = new(StringComparer.Ordinal);
    private readonly string _redisPrefix = $"cm-test:{Guid.NewGuid():N}:";

    public ClientManagerApiFactory Configure(Action<IDictionary<string, string?>> configure)
    {
        configure(_settings);
        return this;
    }

    public HttpClient CreateClientWithBaseAddress()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.BaseAddress = new Uri("https://localhost");
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var defaults = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Persistence:DefaultProvider"] = "Redis",
                ["Persistence:DefaultRedis:Host"] = "localhost",
                ["Persistence:DefaultRedis:Port"] = "6379",
                ["Persistence:DefaultRedis:GlobalKeyPrefix"] = _redisPrefix,
                ["Seed:SeedApiEnabled"] = "false",
                ["Observability:OtlpEndpoint"] = string.Empty
            };

            foreach (var (key, value) in _settings)
            {
                defaults[key] = value;
            }

            config.AddInMemoryCollection(defaults!);
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}

[CollectionDefinition(ApiIntegrationCollection.Name)]
public sealed class ApiIntegrationCollection : ICollectionFixture<ClientManagerApiFactory>
{
    public const string Name = "api-integration";
}
