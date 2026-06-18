using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Stores.Implementations;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using Xunit;

namespace ClientManager.IntegrationTests;

public class MultiPodConcurrencyTests
{
    [Fact]
    public async Task ConcurrentUsageCounterIncrements_AreNotLost()
    {
        var store = CreateTempJsonStore();
        var options = new UsageTrackingOptions();
        var database = new UsageCounterDatabase(store, options);
        var bucketTimestamp = new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc);
        var key = new UsageCounterKey("client-a", TargetType.Service, "svc-1", BucketGranularity.Second, bucketTimestamp, UsageEventType.Granted);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => database.IncrementBucketCountsAsync(
                new Dictionary<UsageCounterKey, long> { [key] = 1 }))
            .ToArray();

        await Task.WhenAll(tasks);

        var values = await database.GetBucketCountsAsync([key]);
        Assert.Equal(20, values[key]);
    }

    [Fact]
    public async Task TryIncrementWithinLimits_EnforcesPoolCap()
    {
        var store = CreateTempJsonStore();
        var poolKey = "alloc-count:pool:pool-1";
        var clientKey = "alloc-count:client:pool-1:client-a";

        var successes = 0;
        var tasks = Enumerable.Range(0, 10)
            .Select(async _ =>
            {
                if (await store.TryIncrementWithinLimitsAsync(
                    [
                        (poolKey, 1, TimeSpan.FromHours(1)),
                        (clientKey, 5, TimeSpan.FromHours(1))
                    ]))
                {
                    Interlocked.Increment(ref successes);
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, successes);
        Assert.Equal(1, await store.GetCounterAsync(poolKey));
        Assert.Equal(1, await store.GetCounterAsync(clientKey));
    }

    [Fact]
    public async Task TryConsumeTokenBucket_RespectsMaxRequests()
    {
        var store = CreateTempJsonStore();
        const string tokensKey = "bucket:test:tokens";
        const string refillKey = "bucket:test:lastrefill";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var allowed = 0;
        var tasks = Enumerable.Range(0, 10)
            .Select(async _ =>
            {
                var result = await store.TryConsumeTokenBucketAsync(
                    tokensKey,
                    refillKey,
                    bucketCapacity: 3,
                    tokensPerRefill: 3,
                    refillIntervalSeconds: 60,
                    stateWindow: TimeSpan.FromMinutes(10),
                    nowUnixSeconds: now);

                if (result.IsAllowed)
                {
                    Interlocked.Increment(ref allowed);
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(3, allowed);
    }

    [Fact]
    public async Task SetIfFieldEquals_OnlyUpdatesOnce()
    {
        var store = CreateTempJsonStore();
        const string collection = "ResourceAllocation";
        var allocation = new ResourceAllocation
        {
            Id = "alloc-1",
            ClientId = "client-a",
            ResourcePoolId = "pool-1",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            IsReleased = false
        };

        await store.SetAsync(collection, allocation.Id, allocation);

        var first = await store.SetIfFieldEqualsAsync(
            collection,
            allocation.Id,
            allocation with { IsReleased = true },
            nameof(ResourceAllocation.IsReleased),
            false);
        var second = await store.SetIfFieldEqualsAsync(
            collection,
            allocation.Id,
            allocation with { IsReleased = true },
            nameof(ResourceAllocation.IsReleased),
            false);

        Assert.True(first);
        Assert.False(second);
    }

    private static JsonFileDocumentStore CreateTempJsonStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), "clientmanager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new JsonFileDocumentStore(directory);
    }
}
