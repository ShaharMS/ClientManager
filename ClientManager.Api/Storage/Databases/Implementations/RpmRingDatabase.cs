using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Api.Storage.Stores.Interfaces;

namespace ClientManager.Api.Storage.Databases.Implementations;

/// <summary>
/// RPM ring persistence backed by atomic counter increments.
/// </summary>
public sealed class RpmRingDatabase(IDocumentStore store) : IRpmRingDatabase
{
    private const string KeyPrefix = "rpm:";

    public async Task IncrementBucketsAsync(IReadOnlyDictionary<string, long> buckets, TimeSpan retention, CancellationToken cancellationToken = default)
    {
        if (buckets.Count == 0)
        {
            return;
        }

        await store.IncrementManyCountersAsync(
            buckets.ToDictionary(
                pair => $"{KeyPrefix}{pair.Key}",
                pair => (pair.Value, retention),
                StringComparer.Ordinal),
            cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, long>> GetBucketsByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default) =>
        store.GetCountersByPrefixAsync($"{KeyPrefix}{keyPrefix}", cancellationToken);
}
