using ClientManager.Api.Services.Storage;
using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using Microsoft.Extensions.Options;

namespace ClientManager.Tests.Unit;

public sealed class RpmAccountingServiceTests
{
    [Fact]
    public async Task GetRequestsPerMinuteAsync_sums_last_sixty_seconds_without_averaging()
    {
        var database = new InMemoryRpmRingDatabase();
        using var service = new RpmAccountingService(
            database,
            Options.Create(new RpmOptions { FlushEventCount = 1 }));

        for (var i = 0; i < 7; i++)
        {
            service.RecordRequest();
        }

        var rpm = await service.GetRequestsPerMinuteAsync();

        Assert.Equal(7, rpm);
    }

    private sealed class InMemoryRpmRingDatabase : IRpmRingDatabase
    {
        private readonly Dictionary<string, long> _buckets = new(StringComparer.Ordinal);

        public Task IncrementBucketsAsync(
            IReadOnlyDictionary<string, long> buckets,
            TimeSpan retention,
            CancellationToken cancellationToken = default)
        {
            foreach (var (key, value) in buckets)
            {
                var storageKey = $"rpm:{key}";
                _buckets[storageKey] = _buckets.GetValueOrDefault(storageKey) + value;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, long>> GetBucketsByPrefixAsync(
            string keyPrefix,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, long>>(_buckets);
    }
}
