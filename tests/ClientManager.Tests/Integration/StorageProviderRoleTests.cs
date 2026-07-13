using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Enums;
using ClientManager.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ClientManager.Tests.Integration;

public sealed class StorageProviderRoleTests : IAsyncLifetime
{
    private MongoRoleApiFactory? _factory;

    [MongoIntegrationFact]
    public void Mongo_is_default_and_rate_limiting_can_bind_to_redis()
    {
        Assert.NotNull(_factory);

        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;

        Assert.Equal(PersistenceProvider.MongoDb, options.DefaultProvider);
        Assert.NotNull(options.Roles);
        Assert.Equal(PersistenceProvider.Redis, options.Roles![StorageRole.RateLimiting].Provider);
        Assert.Equal(PersistenceProvider.MongoDb, options.Roles[StorageRole.Configuration].Provider);
    }

    public Task InitializeAsync()
    {
        if (!MongoIntegrationGate.IsAvailable)
        {
            return Task.CompletedTask;
        }

        _factory = new MongoRoleApiFactory($"cm-role-test-{Guid.NewGuid():N}");
        _ = _factory.Services;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private sealed class MongoRoleApiFactory(string databaseName) : WebApplicationFactory<ClientManager.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["Persistence:DefaultProvider"] = "MongoDb",
                    ["Persistence:DefaultMongoDb:ConnectionString"] = MongoIntegrationGate.ConnectionString,
                    ["Persistence:DefaultMongoDb:DatabaseName"] = databaseName,
                    ["Persistence:Roles:Configuration:Provider"] = "MongoDb",
                    ["Persistence:Roles:Configuration:MongoDb:ConnectionString"] = MongoIntegrationGate.ConnectionString,
                    ["Persistence:Roles:Configuration:MongoDb:DatabaseName"] = databaseName,
                    ["Persistence:Roles:RateLimiting:Provider"] = "Redis",
                    ["Persistence:Roles:RateLimiting:Redis:Host"] = "localhost",
                    ["Persistence:Roles:RateLimiting:Redis:Port"] = "6379",
                    ["Persistence:Roles:RateLimiting:Redis:DatabaseIndex"] = "9",
                    ["Persistence:Roles:RateLimiting:Redis:GlobalKeyPrefix"] = $"cm-role:{Guid.NewGuid():N}:",
                    ["Persistence:Roles:Rpm:Provider"] = "Redis",
                    ["Persistence:Roles:Rpm:Redis:Host"] = "localhost",
                    ["Persistence:Roles:Rpm:Redis:Port"] = "6379",
                    ["Persistence:Roles:Rpm:Redis:DatabaseIndex"] = "8",
                    ["Persistence:Roles:Rpm:Redis:GlobalKeyPrefix"] = $"cm-rpm:{Guid.NewGuid():N}:",
                    ["Seed:SeedApiEnabled"] = "false",
                    ["Observability:OtlpEndpoint"] = string.Empty
                });
            });
        }
    }
}
