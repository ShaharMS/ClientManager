namespace ClientManager.Api.Storage.Databases.Interfaces;

/// <summary>
/// Persists the global RPM second-bucket ring.
/// </summary>
public interface IRpmRingDatabase
{
    Task IncrementBucketsAsync(IReadOnlyDictionary<string, long> buckets, TimeSpan retention, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, long>> GetBucketsByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default);
}
