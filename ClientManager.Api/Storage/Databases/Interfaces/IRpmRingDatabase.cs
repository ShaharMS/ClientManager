namespace ClientManager.Api.Storage.Databases.Interfaces;

/// <summary>
/// Persists the global RPM second-bucket ring shared across API replicas.
/// </summary>
/// <remarks>
/// <para>
/// Each granted access check increments a UTC second bucket. Buckets are retained for a configured
/// window and aggregated into a five-minute requests-per-minute average for the dashboard.
/// </para>
/// <para>
/// Writes are batched per replica in <c>RpmAccountingService</c> before reaching this database so
/// the hot path avoids per-request storage round trips while accepting a small crash/lag tradeoff.
/// </para>
/// </remarks>
public interface IRpmRingDatabase
{
    /// <summary>
    /// Atomically increments one or more second buckets and expires keys outside the retention window.
    /// </summary>
    /// <param name="buckets">Bucket keys and increment amounts produced by the local buffer flush.</param>
    /// <param name="retention">How long bucket keys remain readable for RPM calculation.</param>
    /// <param name="cancellationToken">Cancels the write before all buckets are persisted.</param>
    Task IncrementBucketsAsync(IReadOnlyDictionary<string, long> buckets, TimeSpan retention, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all bucket counters whose keys start with the given prefix.
    /// </summary>
    /// <param name="keyPrefix">Prefix identifying the RPM ring namespace.</param>
    /// <param name="cancellationToken">Cancels the read if the store is unresponsive.</param>
    /// <returns>Bucket keys and their current counts.</returns>
    Task<IReadOnlyDictionary<string, long>> GetBucketsByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default);
}
