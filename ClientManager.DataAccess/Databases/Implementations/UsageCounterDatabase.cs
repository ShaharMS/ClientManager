using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Persists usage event counts as atomic TTL-backed counters in <see cref="IDocumentStore"/>.
/// </summary>
public class UsageCounterDatabase : IUsageCounterDatabase
{
    private readonly IDocumentStore _store;
    private readonly UsageTrackingOptions _options;

    public UsageCounterDatabase(IDocumentStore store, UsageTrackingOptions options)
    {
        _store = store;
        _options = options;
    }

    /// <inheritdoc />
    public async Task IncrementBucketCountsAsync(
        IReadOnlyDictionary<UsageCounterKey, long> deltas,
        CancellationToken cancellationToken = default)
    {
        if (deltas.Count == 0)
        {
            return;
        }

        var entries = deltas.ToDictionary(
            entry => BuildStorageKey(entry.Key),
            entry => (entry.Value, GetRetentionWindow(entry.Key.Granularity)),
            StringComparer.Ordinal);

        await _store.IncrementManyCountersAsync(entries, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<UsageCounterKey, long>> GetBucketCountsAsync(
        IEnumerable<UsageCounterKey> keys,
        CancellationToken cancellationToken = default)
    {
        var keyList = keys.Distinct().ToList();
        if (keyList.Count == 0)
        {
            return new Dictionary<UsageCounterKey, long>();
        }

        var storageKeys = keyList.ToDictionary(
            key => BuildStorageKey(key),
            key => key,
            StringComparer.Ordinal);
        var values = await _store.GetManyCountersAsync(storageKeys.Keys, cancellationToken);

        var result = new Dictionary<UsageCounterKey, long>(keyList.Count);
        foreach (var (storageKey, counterKey) in storageKeys)
        {
            result[counterKey] = values.TryGetValue(storageKey, out var value) ? value : 0;
        }

        return result;
    }

    /// <inheritdoc />
    public Task ResetBucketCountsAsync(
        IEnumerable<UsageCounterKey> keys,
        CancellationToken cancellationToken = default)
    {
        var tasks = keys
            .Distinct()
            .Select(key => _store.ResetCounterAsync(BuildStorageKey(key), cancellationToken));

        return Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public IReadOnlyList<UsageCounterKey> BuildKeysForRange(
        string clientId,
        TargetType targetType,
        string targetId,
        DateTime from,
        DateTime to,
        params UsageEventType[] eventTypes)
    {
        if (eventTypes.Length == 0)
        {
            eventTypes = [UsageEventType.Granted, UsageEventType.Denied, UsageEventType.Released];
        }

        var keys = new List<UsageCounterKey>();
        var cursor = RoundDownToSecond(from);
        var end = RoundDownToSecond(to);

        while (cursor <= end)
        {
            foreach (var eventType in eventTypes)
            {
                keys.Add(new UsageCounterKey(
                    clientId,
                    targetType,
                    targetId,
                    BucketGranularity.Second,
                    cursor,
                    eventType));
            }

            cursor = cursor.AddSeconds(1);
        }

        return keys;
    }

    /// <summary>
    /// Builds the storage counter key for a usage counter.
    /// </summary>
    public static string BuildStorageKey(UsageCounterKey key)
    {
        return $"usage:{key.ClientId}:{key.TargetType}:{key.TargetId}:{key.Granularity}:{key.BucketTimestamp:yyyyMMddHHmmss}:{key.EventType}";
    }

    private TimeSpan GetRetentionWindow(BucketGranularity granularity)
    {
        return granularity switch
        {
            BucketGranularity.Second => _options.SecondRetention,
            BucketGranularity.FiveMinute => _options.FiveMinuteRetention,
            BucketGranularity.Hour => _options.HourlyRetention,
            BucketGranularity.Day => _options.DailyRetention,
            _ => _options.SecondRetention
        };
    }

    private static DateTime RoundDownToSecond(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
    }
}
