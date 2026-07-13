using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Instrumentation;
using ClientManager.Api.Services.Storage.RateLimiting.Strategies;
using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Tests.Helpers;
using Microsoft.Extensions.Options;

namespace ClientManager.Tests.Unit;

public sealed class RateLimitStrategyTests
{
    private readonly InMemoryRateLimitStateDatabase _database = new();
    private readonly StorageMetrics _metrics = new(new TestMeterFactory());
    private readonly IOptions<RateLimitingSettings> _settings =
        Options.Create(new RateLimitingSettings { WindowAlignmentAnchor = TimeSpan.Zero });

    [Fact]
    public async Task FixedWindow_allows_up_to_max_then_denies()
    {
        var strategy = new FixedWindowStrategy(_database, _metrics, _settings);
        var limit = new RateLimitPolicy
        {
            Strategy = RateLimitStrategy.FixedWindow,
            MaxRequests = 2,
            Window = TimeSpan.FromMinutes(1)
        };

        var first = await strategy.EvaluateAsync("client:svc", limit);
        var second = await strategy.EvaluateAsync("client:svc", limit);
        var third = await strategy.EvaluateAsync("client:svc", limit);

        Assert.True(first.IsAllowed);
        Assert.True(second.IsAllowed);
        Assert.False(third.IsAllowed);
        Assert.NotNull(third.RetryAfterSeconds);
    }

    [Fact]
    public async Task ApproximateSlidingWindow_peek_does_not_increment()
    {
        var strategy = new ApproximateSlidingWindowStrategy(_database, _metrics);
        var limit = new RateLimitPolicy
        {
            Strategy = RateLimitStrategy.ApproximateSlidingWindow,
            MaxRequests = 1,
            Window = TimeSpan.FromMinutes(1)
        };

        await strategy.EvaluateAsync("sliding:client", limit);
        var peek = await strategy.PeekAsync("sliding:client", limit);

        Assert.False(peek.IsAllowed);
    }

    [Fact]
    public async Task TokenBucket_allows_initial_capacity_then_denies()
    {
        var strategy = new TokenBucketStrategy(_database, _metrics, _settings);
        var limit = new RateLimitPolicy
        {
            Strategy = RateLimitStrategy.TokenBucket,
            MaxRequests = 2,
            Window = TimeSpan.FromMinutes(1),
            TokensPerRefill = 1
        };

        var first = await strategy.EvaluateAsync("bucket:client", limit);
        var second = await strategy.EvaluateAsync("bucket:client", limit);
        var third = await strategy.EvaluateAsync("bucket:client", limit);

        Assert.True(first.IsAllowed);
        Assert.True(second.IsAllowed);
        Assert.False(third.IsAllowed);
    }
}
