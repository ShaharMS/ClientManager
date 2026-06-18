using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Atomic counters for usage events before they are rolled into <see cref="UsageSnapshot"/> documents.
/// </summary>
public interface IUsageCounterDatabase
{
    /// <summary>
    /// Atomically increments usage counters for the supplied deltas.
    /// </summary>
    Task IncrementBucketCountsAsync(
        IReadOnlyDictionary<UsageCounterKey, long> deltas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads current values for the supplied counter keys.
    /// </summary>
    Task<IReadOnlyDictionary<UsageCounterKey, long>> GetBucketCountsAsync(
        IEnumerable<UsageCounterKey> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the supplied counter keys to zero.
    /// </summary>
    Task ResetBucketCountsAsync(
        IEnumerable<UsageCounterKey> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds counter keys for every second bucket in the inclusive-exclusive range.
    /// </summary>
    IReadOnlyList<UsageCounterKey> BuildKeysForRange(
        string clientId,
        TargetType targetType,
        string targetId,
        DateTime from,
        DateTime to,
        params UsageEventType[] eventTypes);
}
