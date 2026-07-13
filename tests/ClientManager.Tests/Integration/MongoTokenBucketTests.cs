using System.Net;
using ClientManager.Api.Storage.Stores.Implementations;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ClientManager.Tests.Integration;

public sealed class MongoTokenBucketTests : IAsyncLifetime
{
    private readonly string _databaseName = $"cm-mongo-test-{Guid.NewGuid():N}";
    private MongoClientApiFactory? _factory;
    private HttpClient? _client;

    [MongoIntegrationFact]
    public async Task Mongo_token_bucket_enforces_capacity_under_parallel_load()
    {
        Assert.NotNull(_client);

        const string serviceId = "mongo-bucket-svc";
        const string clientId = "mongo-bucket-client";
        await TestCatalogFactory.SeedServiceAsync(_client, TestCatalogFactory.CreateService(serviceId, "Mongo Bucket"));
        await TestCatalogFactory.SeedClientAsync(
            _client,
            TestCatalogFactory.CreateClient(
                clientId,
                serviceId,
                rateLimit: new RateLimitPolicy
                {
                    Strategy = RateLimitStrategy.TokenBucket,
                    MaxRequests = 5,
                    Window = TimeSpan.FromMinutes(1),
                    TokensPerRefill = 1
                }));

        var tasks = Enumerable.Range(0, 25)
            .Select(_ => _client.GetAsync($"api/v1/access/check?clientId={clientId}&serviceId={serviceId}"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        var granted = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        var limited = responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests);

        Assert.Equal(5, granted);
        Assert.Equal(20, limited);

        var counters = new MongoClient(MongoIntegrationGate.ConnectionString)
            .GetDatabase(_databaseName)
            .GetCollection<BsonDocument>("_counters");
        var tokensKey = $"bucket:{clientId}:{serviceId}:tokens";
        var refillKey = $"bucket:{clientId}:{serviceId}:lastrefill";
        var tokenState = await counters.Find(Builders<BsonDocument>.Filter.Eq("_id", tokensKey)).SingleAsync();
        var refillMirror = await counters.Find(Builders<BsonDocument>.Filter.Eq("_id", refillKey)).SingleAsync();

        Assert.Equal(0L, tokenState["Count"].AsInt64);
        Assert.True(tokenState["LastRefill"].AsInt64 > 0);
        Assert.Equal(tokenState["LastRefill"].AsInt64, refillMirror["Count"].AsInt64);
    }

    [MongoIntegrationFact]
    public async Task Mongo_token_bucket_migrates_legacy_state_and_refills_on_aligned_boundary()
    {
        var database = new MongoClient(MongoIntegrationGate.ConnectionString).GetDatabase(_databaseName);
        var counters = database.GetCollection<BsonDocument>("_counters");
        const string tokensKey = "bucket:legacy:tokens";
        const string refillKey = "bucket:legacy:lastrefill";
        var windowStart = DateTime.UtcNow;
        await counters.InsertManyAsync(
        [
            new BsonDocument { ["_id"] = tokensKey, ["Count"] = 2L, ["WindowStart"] = windowStart },
            new BsonDocument { ["_id"] = refillKey, ["Count"] = 120L, ["WindowStart"] = windowStart }
        ]);

        var store = new MongoDBDocumentStore(database);
        var first = await store.TryConsumeTokenBucketAsync(tokensKey, refillKey, 5, 1, 60, TimeSpan.FromMinutes(10), 125);
        var second = await store.TryConsumeTokenBucketAsync(tokensKey, refillKey, 5, 1, 60, TimeSpan.FromMinutes(10), 125);
        var denied = await store.TryConsumeTokenBucketAsync(tokensKey, refillKey, 5, 1, 60, TimeSpan.FromMinutes(10), 125);
        await counters.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", tokensKey),
            Builders<BsonDocument>.Update
                .Set("Count", new BsonDouble(0))
                .Set("LastRefill", new BsonDouble(120)));
        var refilled = await store.TryConsumeTokenBucketAsync(tokensKey, refillKey, 5, 1, 60, TimeSpan.FromMinutes(10), 180);

        Assert.Equal((true, 1L, 0L), first);
        Assert.Equal((true, 0L, 0L), second);
        Assert.Equal((false, 0L, 55L), denied);
        Assert.Equal((true, 0L, 0L), refilled);

        var tokenState = await counters.Find(Builders<BsonDocument>.Filter.Eq("_id", tokensKey)).SingleAsync();
        var refillMirror = await counters.Find(Builders<BsonDocument>.Filter.Eq("_id", refillKey)).SingleAsync();
        Assert.Equal(180L, tokenState["LastRefill"].AsInt64);
        Assert.Equal(180L, refillMirror["Count"].AsInt64);
    }

    public Task InitializeAsync()
    {
        if (!MongoIntegrationGate.IsAvailable)
        {
            return Task.CompletedTask;
        }

        _factory = new MongoClientApiFactory(_databaseName);
        _client = _factory.CreateClientWithBaseAddress();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            await new MongoClient(MongoIntegrationGate.ConnectionString)
                .DropDatabaseAsync(_databaseName);
        }
    }

    private sealed class MongoClientApiFactory(string databaseName) : WebApplicationFactory<ClientManager.Api.Program>
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
                    ["Persistence:Roles:RateLimiting:Provider"] = "MongoDb",
                    ["Persistence:Roles:RateLimiting:MongoDb:ConnectionString"] = MongoIntegrationGate.ConnectionString,
                    ["Persistence:Roles:RateLimiting:MongoDb:DatabaseName"] = databaseName,
                    ["Persistence:Roles:Rpm:Provider"] = "MongoDb",
                    ["Persistence:Roles:Rpm:MongoDb:ConnectionString"] = MongoIntegrationGate.ConnectionString,
                    ["Persistence:Roles:Rpm:MongoDb:DatabaseName"] = databaseName,
                    ["Seed:SeedApiEnabled"] = "false",
                    ["Observability:OtlpEndpoint"] = string.Empty
                });
            });
        }

        public HttpClient CreateClientWithBaseAddress()
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            client.BaseAddress = new Uri("https://localhost");
            return client;
        }
    }
}
