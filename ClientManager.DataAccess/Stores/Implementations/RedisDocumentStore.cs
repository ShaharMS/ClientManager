using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using ClientManager.DataAccess.Stores.Implementations.Helpers;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using static ClientManager.DataAccess.Stores.Implementations.Helpers.StoreSerialization;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using NRedisStack.Search.Literals.Enums;
using StackExchange.Redis;

namespace ClientManager.DataAccess.Stores.Implementations;

/// <summary>
/// Redis-based implementation of <see cref="IDocumentStore"/>.
/// Each collection maps to a Redis hash. Counters use native Redis INCR with key expiry.
/// When the RediSearch module is detected, <see cref="SearchAsync{T}"/> and <see cref="CountAsync{T}"/>
/// use native <c>FT.SEARCH</c> commands for server-side filtering. Otherwise, search falls back
/// to <see cref="InMemoryQueryEvaluator"/>.
/// </summary>
public class RedisDocumentStore : IDocumentStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly int _databaseIndex;
    private readonly string _globalKeyPrefix;
    private readonly bool _hasRediSearch;
    private readonly ConcurrentDictionary<string, bool> _createdIndexes = new();

    /// <summary>
    /// Initializes a new instance of <see cref="RedisDocumentStore"/>.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="databaseIndex">The Redis logical database index to use for all operations.</param>
    /// <param name="globalKeyPrefix">An optional prefix prepended to every Redis key created by this store.</param>
    public RedisDocumentStore(IConnectionMultiplexer redis, int databaseIndex, string? globalKeyPrefix = null)
    {
        _redis = redis;
        _databaseIndex = databaseIndex;
        _globalKeyPrefix = globalKeyPrefix ?? string.Empty;
        _hasRediSearch = DetectRediSearch();
    }

    private IDatabase Database => _redis.GetDatabase(_databaseIndex);

    private const string CounterPrefix = "counter:";
    private const string UsageCounterIndexKeySuffix = "counter:index:usage";
    private const int CounterScanPageSize = 250;
    private const string JsonRootPath = "$";
    private const string DecrementCounterScript = """
local ttl = redis.call('PTTL', KEYS[1])
if ttl == -2 then
    return 0
end
local current = tonumber(redis.call('GET', KEYS[1]) or '0')
local amount = tonumber(ARGV[1])
if amount <= 0 then
    return current
end
local next = current - amount
if next < 0 then
    next = 0
end
redis.call('SET', KEYS[1], next)
if ttl > 0 then
    redis.call('PEXPIRE', KEYS[1], ttl)
end
return next
""";

    private const string TryConsumeTokenBucketScript = """
local function getCounter(key)
  local value = redis.call('GET', key)
  if not value then return 0 end
  return tonumber(value)
end

local function setCounter(key, value, ttlMs)
  redis.call('SET', key, value)
  if ttlMs > 0 then redis.call('PEXPIRE', key, ttlMs) end
end

local bucketCapacity = tonumber(ARGV[1])
local tokensPerRefill = tonumber(ARGV[2])
local refillInterval = tonumber(ARGV[3])
local ttlMs = tonumber(ARGV[4])
local now = tonumber(ARGV[5])

local storedTokens = getCounter(KEYS[1])
local lastRefill = getCounter(KEYS[2])

if lastRefill == 0 then
  local initial = bucketCapacity - 1
  setCounter(KEYS[1], initial, ttlMs)
  setCounter(KEYS[2], now, ttlMs)
  return {1, initial, 0}
end

local alignedNow = math.floor(now / refillInterval) * refillInterval
local alignedLast = math.floor(lastRefill / refillInterval) * refillInterval
local intervals = math.floor((alignedNow - alignedLast) / refillInterval)
local tokensToAdd = intervals * tokensPerRefill
local tokens = math.min(bucketCapacity, storedTokens + tokensToAdd)
local newLastRefill = intervals > 0 and alignedNow or lastRefill

if tokens <= 0 then
  setCounter(KEYS[1], 0, ttlMs)
  setCounter(KEYS[2], newLastRefill, ttlMs)
  local retry = math.max(1, refillInterval - (now - alignedNow))
  return {0, 0, retry}
end

local remaining = tokens - 1
setCounter(KEYS[1], remaining, ttlMs)
setCounter(KEYS[2], newLastRefill, ttlMs)
return {1, remaining, 0}
""";

    private string HashKey(string collection) => PrefixKey($"collection:{collection}");

    private string DocKey(string collection, string id) => PrefixKey($"doc:{collection}:{id}");

    private string IndexName(string collection) => PrefixKey($"idx:{collection}");

    private RedisKey CounterKey(string key) => PrefixKey($"{CounterPrefix}{key}");

    private RedisKey UsageCounterIndexKey() => PrefixKey(UsageCounterIndexKeySuffix);

    private static bool IsUsageCounterKey(string key) =>
        key.StartsWith("usage:", StringComparison.Ordinal);

    private string PrefixKey(string key) => string.IsNullOrEmpty(_globalKeyPrefix)
        ? key
        : $"{_globalKeyPrefix}{key}";

    private string StorageMode => _hasRediSearch ? "json-search" : "hash";

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class
    {
        return await ExecuteWithRedisContextAsync(
            "get",
            async () =>
            {
                if (_hasRediSearch)
                {
                    var json = await Database.JSON().GetAsync(DocKey(collection, id), JsonRootPath);
                    if (json is null || json.IsNull)
                        return null;

                    return DeserializeRedisJson<T>(json.ToString());
                }

                var value = await Database.HashGetAsync(HashKey(collection), id);
                return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
            },
            DescribeCollectionContext(collection, id));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetManyAsync<T>(
        string collection,
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) where T : class
    {
        var requestedIds = ids.Distinct(StringComparer.Ordinal).ToArray();
        if (requestedIds.Length == 0)
        {
            return [];
        }

        return await ExecuteWithRedisContextAsync(
            "get_many",
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return _hasRediSearch
                    ? await GetManyFromJsonAsync<T>(collection, requestedIds, cancellationToken)
                    : await GetManyFromHashAsync<T>(collection, requestedIds, cancellationToken);
            },
            DescribeCollectionContext(collection, null, requestedIds.Length));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class
    {
        return await ExecuteWithRedisContextAsync(
            "get_all",
            async () =>
            {
                if (_hasRediSearch)
                {
                    EnsureIndex(collection);
                    var search = Database.FT();
                    var result = search.Search(IndexName(collection), new Query("*").SetNoContent(false).Limit(0, int.MaxValue));

                    var items = new List<T>(result.Documents.Count);
                    foreach (var doc in result.Documents)
                    {
                        var jsonField = doc["$"];
                        if (jsonField == RedisValue.Null) continue;
                        var json = jsonField.ToString();
                        var item = JsonSerializer.Deserialize<T>(json, JsonOptions);
                        if (item is not null)
                            items.Add(item);
                    }

                    return items;
                }

                var entries = await Database.HashGetAllAsync(HashKey(collection));
                return [.. entries.Select(e => JsonSerializer.Deserialize<T>(e.Value.ToString(), JsonOptions)!)];
            },
            DescribeCollectionContext(collection));
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class
    {
        await ExecuteWithRedisContextAsync(
            "set",
            async () =>
            {
                var json = JsonSerializer.Serialize(document, JsonOptions);

                if (_hasRediSearch)
                {
                    EnsureIndex(collection);
                    await Database.JSON().SetAsync(DocKey(collection, id), JsonRootPath, json);
                }
                else
                {
                    await Database.HashSetAsync(HashKey(collection), id, json);
                }
            },
            DescribeCollectionContext(collection, id));
    }

    /// <inheritdoc />
    public async Task SetManyAsync<T>(
        string collection,
        IReadOnlyDictionary<string, T> documents,
        CancellationToken cancellationToken = default) where T : class
    {
        if (documents.Count == 0)
            return;

        await ExecuteWithRedisContextAsync(
            "set_many",
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_hasRediSearch)
                {
                    await SetManyJsonDocumentsAsync(collection, documents);
                    return;
                }

                var entries = documents.Select(entry =>
                    new HashEntry(entry.Key, JsonSerializer.Serialize(entry.Value, JsonOptions))).ToArray();
                await Database.HashSetAsync(HashKey(collection), entries);
            },
            DescribeCollectionContext(collection, null, documents.Count));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRedisContextAsync(
            "delete",
            async () =>
            {
                if (_hasRediSearch)
                {
                    await Database.KeyDeleteAsync(DocKey(collection, id));
                }
                else
                {
                    await Database.HashDeleteAsync(HashKey(collection), id);
                }
            },
            DescribeCollectionContext(collection, id));
    }

    /// <inheritdoc />
    public async Task<(bool IsAllowed, long RemainingTokens, long RetryAfterSeconds)> TryConsumeTokenBucketAsync(
        string tokensKey,
        string lastRefillKey,
        int bucketCapacity,
        int tokensPerRefill,
        long refillIntervalSeconds,
        TimeSpan stateWindow,
        long nowUnixSeconds,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRedisContextAsync(
            "counter_try_consume_token_bucket",
            async () =>
            {
                var result = (RedisResult[])(await Database.ScriptEvaluateAsync(
                    TryConsumeTokenBucketScript,
                    new RedisKey[] { CounterKey(tokensKey), CounterKey(lastRefillKey) },
                    new RedisValue[]
                    {
                        bucketCapacity,
                        tokensPerRefill,
                        refillIntervalSeconds,
                        (long)stateWindow.TotalMilliseconds,
                        nowUnixSeconds
                    }))!;

                return ((long)result[0] == 1, (long)result[1], (long)result[2]);
            },
            DescribeCounterBatchContext([tokensKey, lastRefillKey]));
    }

    /// <inheritdoc />
    public async Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRedisContextAsync(
            "counter_increment",
            async () =>
            {
                var redisKey = CounterKey(key);
                var db = Database;

                var count = await db.StringIncrementAsync(redisKey);

                if (count == 1)
                {
                    await db.KeyExpireAsync(redisKey, window);
                }

                if (IsUsageCounterKey(key))
                {
                    await db.SetAddAsync(UsageCounterIndexKey(), key);
                }

                return count;
            },
            DescribeCounterContext(key, window));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> IncrementManyCountersAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return new Dictionary<string, long>();

        return await ExecuteWithRedisContextAsync(
            "counter_increment_many",
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = Database.CreateBatch();
                var tasks = entries.ToDictionary(
                    entry => entry.Key,
                    entry => batch.StringIncrementAsync(CounterKey(entry.Key), entry.Value.amount),
                    StringComparer.Ordinal);

                batch.Execute();
                return await CompleteIncrementBatchAsync(entries, tasks, cancellationToken);
            },
            DescribeCounterBatchContext(entries.Keys));
    }

    /// <inheritdoc />
    public async Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRedisContextAsync(
            "counter_get",
            async () =>
            {
                var redisKey = CounterKey(key);
                var value = await Database.StringGetAsync(redisKey);
                return value.IsNullOrEmpty ? 0 : (long)value;
            },
            DescribeCounterContext(key));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> GetManyCountersAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var requestedKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
        if (requestedKeys.Length == 0)
            return new Dictionary<string, long>();

        return await ExecuteWithRedisContextAsync(
            "counter_get_many",
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var redisKeys = requestedKeys.Select(CounterKey).ToArray();
                var values = await Database.StringGetAsync(redisKeys);
                return MapCounterValues(requestedKeys, values);
            },
            DescribeCounterBatchContext(requestedKeys));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> GetCountersByPrefixAsync(
        string keyPrefix,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRedisContextAsync<IReadOnlyDictionary<string, long>>(
            "counter_get_by_prefix",
            async () =>
            {
                if (keyPrefix.StartsWith("usage", StringComparison.Ordinal))
                {
                    var indexMembers = await Database.SetMembersAsync(UsageCounterIndexKey());
                    if (indexMembers.Length > 0)
                    {
                        return await ReadUsageCountersFromIndexMembersAsync(
                            keyPrefix,
                            indexMembers,
                            cancellationToken);
                    }
                }

                return await ScanCountersByPrefixAsync(keyPrefix, populateUsageIndex: true, cancellationToken);
            },
            ("Prefix", keyPrefix));
    }

    private async Task<IReadOnlyDictionary<string, long>> ReadUsageCountersFromIndexMembersAsync(
        string keyPrefix,
        RedisValue[] members,
        CancellationToken cancellationToken)
    {
        var logicalKeys = new List<string>(members.Length);
        foreach (var member in members)
        {
            if (member.IsNullOrEmpty)
            {
                continue;
            }

            var logicalKey = member.ToString();
            if (logicalKey.StartsWith(keyPrefix, StringComparison.Ordinal))
            {
                logicalKeys.Add(logicalKey);
            }
        }

        if (logicalKeys.Count == 0)
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        return await ReadCounterValuesWithIndexCleanupAsync(
            logicalKeys,
            UsageCounterIndexKey(),
            cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, long>> ScanCountersByPrefixAsync(
        string keyPrefix,
        bool populateUsageIndex,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        var pattern = PrefixKey($"{CounterPrefix}{keyPrefix}*");
        var prefixedCounter = PrefixKey(CounterPrefix);
        var indexKey = UsageCounterIndexKey();
        var indexAdds = populateUsageIndex && keyPrefix.StartsWith("usage", StringComparison.Ordinal)
            ? new List<RedisValue>()
            : null;

        foreach (var endpoint in _redis.GetEndPoints())
        {
            var server = _redis.GetServer(endpoint);
            await foreach (var redisKey in server.KeysAsync(
                               database: _databaseIndex,
                               pattern: pattern,
                               pageSize: CounterScanPageSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = await Database.StringGetAsync(redisKey);
                if (value.IsNullOrEmpty)
                {
                    continue;
                }

                var count = (long)value;
                if (count <= 0)
                {
                    continue;
                }

                var logicalKey = redisKey.ToString();
                if (logicalKey.StartsWith(prefixedCounter, StringComparison.Ordinal))
                {
                    logicalKey = logicalKey[prefixedCounter.Length..];
                }

                result[logicalKey] = count;
                indexAdds?.Add(logicalKey);
            }
        }

        if (indexAdds is { Count: > 0 })
        {
            await Database.SetAddAsync(indexKey, indexAdds.ToArray());
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, long>> ReadCounterValuesWithIndexCleanupAsync(
        IReadOnlyList<string> logicalKeys,
        RedisKey indexKey,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, long>(logicalKeys.Count, StringComparer.Ordinal);
        var staleMembers = new List<RedisValue>();

        var redisKeys = logicalKeys.Select(CounterKey).ToArray();
        var values = await Database.StringGetAsync(redisKeys);

        for (var index = 0; index < logicalKeys.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var logicalKey = logicalKeys[index];
            var value = values[index];
            if (value.IsNullOrEmpty || (long)value <= 0)
            {
                staleMembers.Add(logicalKey);
                continue;
            }

            result[logicalKey] = (long)value;
        }

        if (staleMembers.Count > 0)
        {
            // ponytail: lazy index cleanup when TTL expires counters before SREM on decrement
            await Database.SetRemoveAsync(indexKey, staleMembers.ToArray());
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRedisContextAsync(
            "counter_decrement",
            () => DecrementCounterByAsync(key, 1, cancellationToken),
            DescribeCounterContext(key, amount: 1));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> DecrementManyCountersAsync(
        IReadOnlyDictionary<string, long> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return new Dictionary<string, long>();

        return await ExecuteWithRedisContextAsync(
            "counter_decrement_many",
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = Database.CreateBatch();
                var tasks = entries.ToDictionary(
                    entry => entry.Key,
                    entry => batch.ScriptEvaluateAsync(
                        DecrementCounterScript,
                        new[] { CounterKey(entry.Key) },
                        new RedisValue[] { entry.Value }),
                    StringComparer.Ordinal);

                batch.Execute();
                return await CompleteDecrementBatchAsync(tasks, cancellationToken);
            },
            DescribeCounterBatchContext(entries.Keys));
    }

    /// <inheritdoc />
    public async Task ResetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        await ResetManyCountersAsync([key], cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResetManyCountersAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return;
        }

        await ExecuteWithRedisContextAsync(
            "counter_reset_many",
            async () =>
            {
                var keyList = keys as IReadOnlyList<string> ?? keys.ToList();
                var redisKeys = keyList.Select(CounterKey).ToArray();
                await Database.KeyDeleteAsync(redisKeys);

                var usageKeys = keyList
                    .Where(IsUsageCounterKey)
                    .Select(static key => (RedisValue)key)
                    .ToArray();
                if (usageKeys.Length > 0)
                {
                    await Database.SetRemoveAsync(UsageCounterIndexKey(), usageKeys);
                }
            },
            DescribeCounterBatchContext(keys));
    }

    /// <inheritdoc />
    public async Task<int> PurgeCountersByPrefixAsync(
        string keyPrefix,
        Func<string, long, DateTime?, bool> shouldPurge,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRedisContextAsync(
            "counter_purge_by_prefix",
            async () =>
            {
                var counters = keyPrefix.StartsWith("usage", StringComparison.Ordinal)
                    ? await ReadUsageCountersFromIndexMembersAsync(
                        keyPrefix,
                        await Database.SetMembersAsync(UsageCounterIndexKey()),
                        cancellationToken)
                    : await ScanCountersByPrefixAsync(keyPrefix, populateUsageIndex: false, cancellationToken);

                var keysToRemove = new List<string>();
                foreach (var (key, count) in counters)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (shouldPurge(key, count, null))
                    {
                        keysToRemove.Add(key);
                    }
                }

                if (keysToRemove.Count == 0)
                {
                    return 0;
                }

                await Database.KeyDeleteAsync(keysToRemove.Select(CounterKey).ToArray());
                if (keyPrefix.StartsWith("usage", StringComparison.Ordinal))
                {
                    await Database.SetRemoveAsync(
                        UsageCounterIndexKey(),
                        keysToRemove.Select(static key => (RedisValue)key).ToArray());
                }

                return keysToRemove.Count;
            },
            ("Prefix", keyPrefix));
    }

    /// <inheritdoc />
    public async Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRedisContextAsync(
            "counter_set",
            async () =>
            {
                var redisKey = CounterKey(key);
                var db = Database;
                await db.StringSetAsync(redisKey, value, window);
            },
            DescribeCounterContext(key, window, value));
    }

    /// <inheritdoc />
    public async Task SetManyCountersAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return;

        await ExecuteWithRedisContextAsync(
            "counter_set_many",
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = Database.CreateBatch();
                var tasks = entries.Select(entry =>
                    batch.StringSetAsync(CounterKey(entry.Key), entry.Value.value, entry.Value.window)).ToArray();

                batch.Execute();
                await Task.WhenAll(tasks);
            },
            DescribeCounterBatchContext(entries.Keys));
    }

    /// <inheritdoc />
    public async Task<SearchResult<T>> SearchAsync<T>(
        string collection, DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
    {
        return await ExecuteWithRedisContextAsync(
            "search",
            async () =>
            {
                if (!_hasRediSearch)
                {
                    var all = await GetAllAsync<T>(collection, cancellationToken);
                    return InMemoryQueryEvaluator.Apply(all, query);
                }

                EnsureIndex(collection);
                var searchQuery = BuildRediSearchQuery(query);
                var search = Database.FT();
                var result = search.Search(IndexName(collection), searchQuery);

                var items = new List<T>();
                foreach (var doc in result.Documents)
                {
                    var jsonField = doc["$"];
                    if (jsonField == RedisValue.Null) continue;
                    var json = jsonField.ToString();
                    var item = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    if (item is not null)
                        items.Add(item);
                }

                return new SearchResult<T>(items, result.TotalResults);
            },
            DescribeQueryContext(collection, query));
    }

    /// <inheritdoc />
    public async Task<long> CountAsync<T>(
        string collection, DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
    {
        return await ExecuteWithRedisContextAsync(
            "count",
            async () =>
            {
                if (!_hasRediSearch)
                {
                    var all = await GetAllAsync<T>(collection, cancellationToken);
                    return InMemoryQueryEvaluator.Apply(all, query).TotalCount;
                }

                EnsureIndex(collection);
                var searchQuery = BuildRediSearchQuery(query);
                searchQuery.Limit(0, 0);
                var search = Database.FT();
                var result = search.Search(IndexName(collection), searchQuery);

                return result.TotalResults;
            },
            DescribeQueryContext(collection, query));
    }

    private async Task ExecuteWithRedisContextAsync(
        string operation,
        Func<Task> action,
        params (string Name, object? Value)[] context)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RedisException exception)
        {
            EnrichException(exception, operation, context);
            throw;
        }
    }

    private async Task<TResult> ExecuteWithRedisContextAsync<TResult>(
        string operation,
        Func<Task<TResult>> action,
        params (string Name, object? Value)[] context)
    {
        try
        {
            return await action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RedisException exception)
        {
            EnrichException(exception, operation, context);
            throw;
        }
    }

    private bool DetectRediSearch()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var result = server.Execute("MODULE", "LIST");
            if (result is null || result.IsNull)
                return false;

            var modules = (RedisResult[])result!;
            foreach (var module in modules)
            {
                var fields = (RedisResult[])module!;
                for (int i = 0; i < fields.Length - 1; i++)
                {
                    if (fields[i].ToString() == "name" &&
                        fields[i + 1].ToString()!.Equals("search", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task SetManyJsonDocumentsAsync<T>(
        string collection,
        IReadOnlyDictionary<string, T> documents) where T : class
    {
        EnsureIndex(collection);
        foreach (var (id, document) in documents)
        {
            var json = JsonSerializer.Serialize(document, JsonOptions);
            await Database.JSON().SetAsync(DocKey(collection, id), JsonRootPath, json);
        }
    }

    private void EnsureIndex(string collection)
    {
        if (!_hasRediSearch) return;
        if (!_createdIndexes.TryAdd(collection, true)) return;

        try
        {
            var schema = new Schema()
                .AddTextField(new FieldName("$.*", "all_text"), weight: 1.0);

            var parameters = FTCreateParams.CreateParams()
                .On(IndexDataType.JSON)
                .Prefix(DocKey(collection, ""));

            Database.FT().Create(IndexName(collection), parameters, schema);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("Index already exists"))
        {
            // Index already exists — safe to continue
        }
    }

    private void EnrichException(
        RedisException exception,
        string operation,
        params (string Name, object? Value)[] context)
    {
        SetExceptionData(exception, "RedisOperation", operation);
        SetExceptionData(exception, "RedisDatabase", _databaseIndex);
        SetExceptionData(exception, "RedisStorageMode", StorageMode);
        SetExceptionData(exception, "RedisGlobalKeyPrefix", string.IsNullOrEmpty(_globalKeyPrefix) ? "(none)" : _globalKeyPrefix);
        SetExceptionData(exception, "RedisEndpoints", DescribeEndpoints());

        foreach (var (name, value) in context)
        {
            if (value is null)
            {
                continue;
            }

            SetExceptionData(exception, name, value);
        }
    }

    private static void SetExceptionData(Exception exception, string key, object value)
    {
        exception.Data[key] = value;
    }

    private string DescribeEndpoints()
    {
        var endpoints = _redis.GetEndPoints();
        if (endpoints.Length == 0)
        {
            return "(unavailable)";
        }

        return string.Join(",", endpoints.Select(FormatEndpoint));
    }

    private static string FormatEndpoint(EndPoint endpoint)
    {
        return endpoint switch
        {
            DnsEndPoint dns => $"{dns.Host}:{dns.Port}",
            IPEndPoint ip => $"{ip.Address}:{ip.Port}",
            _ => endpoint.ToString() ?? "(unknown)"
        };
    }

    private (string Name, object? Value)[] DescribeCollectionContext(
        string collection,
        string? id = null,
        int? count = null)
    {
        return
        [
            ("RedisCollection", collection),
            ("RedisDocumentId", id),
            ("RedisHashKey", HashKey(collection)),
            ("RedisDocumentKey", id is null ? null : DocKey(collection, id)),
            ("RedisIndexName", _hasRediSearch ? IndexName(collection) : null),
            ("RedisDocumentCount", count)
        ];
    }

    private (string Name, object? Value)[] DescribeCounterContext(
        string key,
        TimeSpan? window = null,
        long? value = null,
        long? amount = null)
    {
        return
        [
            ("RedisCounterName", key),
            ("RedisCounterKey", CounterKey(key).ToString()),
            ("RedisCounterWindow", window?.ToString()),
            ("RedisCounterValue", value),
            ("RedisCounterAmount", amount)
        ];
    }

    private (string Name, object? Value)[] DescribeCounterBatchContext(IEnumerable<string> keys)
    {
        var keyArray = keys.Take(5).ToArray();
        return
        [
            ("RedisCounterCount", keyArray.Length),
            ("RedisCounterSample", string.Join(",", keyArray)),
            ("RedisCounterSampleKeys", string.Join(",", keyArray.Select(key => CounterKey(key).ToString())))
        ];
    }

    private (string Name, object? Value)[] DescribeQueryContext(string collection, DocumentQuery query)
    {
        return
        [
            ("RedisCollection", collection),
            ("RedisHashKey", HashKey(collection)),
            ("RedisIndexName", _hasRediSearch ? IndexName(collection) : null),
            ("RedisTextSearch", query.TextSearch),
            ("RedisFilterCount", query.Filters.Count),
            ("RedisSkip", query.Skip),
            ("RedisTake", query.Take),
            ("RedisSortField", query.Sort?.FieldName),
            ("RedisSortDirection", query.Sort?.Direction.ToString())
        ];
    }

    private static Query BuildRediSearchQuery(DocumentQuery query)
    {
        var parts = new List<string>();

        foreach (var filter in query.Filters)
        {
            parts.Add(TranslateFilterToRediSearch(filter));
        }

        if (!string.IsNullOrEmpty(query.TextSearch))
        {
            parts.Add(EscapeRediSearch(query.TextSearch));
        }

        var queryStr = parts.Count == 0 ? "*" : string.Join(" ", parts);
        var rediSearchQuery = new Query(queryStr);

        if (query.Sort is not null)
        {
            rediSearchQuery.SetSortBy(query.Sort.FieldName, query.Sort.Direction == SortDirection.Ascending);
        }

        var skip = query.Skip ?? 0;
        var take = query.Take ?? int.MaxValue;
        rediSearchQuery.Limit(skip, take);

        return rediSearchQuery;
    }

    private static string TranslateFilterToRediSearch(FilterClause clause)
    {
        var field = clause.FieldName;
        var value = clause.Value?.ToString() ?? "";

        return clause.Operator switch
        {
            FilterOperator.Equals => $"@{field}:{{{EscapeRediSearch(value)}}}",
            FilterOperator.NotEquals => $"-@{field}:{{{EscapeRediSearch(value)}}}",
            FilterOperator.Contains => $"@{field}:*{EscapeRediSearch(value)}*",
            FilterOperator.StartsWith => $"@{field}:{EscapeRediSearch(value)}*",
            FilterOperator.GreaterThan => $"@{field}:[({value} +inf]",
            FilterOperator.GreaterThanOrEqual => $"@{field}:[{value} +inf]",
            FilterOperator.LessThan => $"@{field}:[-inf ({value}]",
            FilterOperator.LessThanOrEqual => $"@{field}:[-inf {value}]",
            _ => "*"
        };
    }

    private static string EscapeRediSearch(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("@", "\\@")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("|", "\\|")
            .Replace("*", "\\*")
            .Replace("-", "\\-")
            .Replace("~", "\\~")
            .Replace("!", "\\!")
            .Replace("'", "\\'");
    }

    private async Task<IReadOnlyList<T>> GetManyFromHashAsync<T>(
        string collection,
        IReadOnlyList<string> requestedIds,
        CancellationToken cancellationToken) where T : class
    {
        var fields = requestedIds.Select(requestedId => (RedisValue)requestedId).ToArray();
        var values = await Database.HashGetAsync(HashKey(collection), fields);
        var results = new List<T>(values.Length);

        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (value.IsNullOrEmpty)
            {
                continue;
            }

            var item = JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
            if (item is not null)
            {
                results.Add(item);
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<T>> GetManyFromJsonAsync<T>(
        string collection,
        IReadOnlyList<string> requestedIds,
        CancellationToken cancellationToken) where T : class
    {
        var batch = Database.CreateBatch();
        var tasks = requestedIds
            .Select(requestedId => batch.ExecuteAsync("JSON.GET", DocKey(collection, requestedId), JsonRootPath))
            .ToArray();

        batch.Execute();
        var values = await Task.WhenAll(tasks);
        var results = new List<T>(values.Length);

        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (value.IsNull)
            {
                continue;
            }

            var item = DeserializeRedisJson<T>(value.ToString());
            if (item is not null)
            {
                results.Add(item);
            }
        }

        return results;
    }

    private static T? DeserializeRedisJson<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        if (!json.StartsWith('['))
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }

        var array = JsonSerializer.Deserialize<JsonElement[]>(json, JsonOptions);
        return array is { Length: > 0 }
            ? JsonSerializer.Deserialize<T>(array[0].GetRawText(), JsonOptions)
            : null;
    }

    private async Task<IReadOnlyDictionary<string, long>> CompleteIncrementBatchAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        IReadOnlyDictionary<string, Task<long>> tasks,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, long>(entries.Count, StringComparer.Ordinal);
        var expiryTasks = new List<Task>(entries.Count);
        var usageIndexAdds = new List<string>();

        foreach (var (key, task) in tasks)
        {
            var count = await task;
            result[key] = count;
            AddExpiryIfNewCounter(entries, key, count, expiryTasks);
            if (IsUsageCounterKey(key) && count > 0)
            {
                usageIndexAdds.Add(key);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (usageIndexAdds.Count > 0)
        {
            await Database.SetAddAsync(
                UsageCounterIndexKey(),
                usageIndexAdds.Select(static key => (RedisValue)key).ToArray());
        }

        await Task.WhenAll(expiryTasks);
        return result;
    }

    private void AddExpiryIfNewCounter(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        string key,
        long count,
        ICollection<Task> expiryTasks)
    {
        var (amount, window) = entries[key];
        if (amount > 0 && count == amount)
            expiryTasks.Add(Database.KeyExpireAsync(CounterKey(key), window));
    }

    private async Task<IReadOnlyDictionary<string, long>> CompleteDecrementBatchAsync(
        IReadOnlyDictionary<string, Task<RedisResult>> tasks,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, long>(tasks.Count, StringComparer.Ordinal);

        foreach (var (key, task) in tasks)
            result[key] = (long)await task;

        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<long> DecrementCounterByAsync(
        string key,
        long amount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await Database.ScriptEvaluateAsync(
            DecrementCounterScript,
            new[] { CounterKey(key) },
            new RedisValue[] { amount });

        return (long)result;
    }

    private static IReadOnlyDictionary<string, long> MapCounterValues(
        IReadOnlyList<string> keys,
        IReadOnlyList<RedisValue> values)
    {
        var result = new Dictionary<string, long>(keys.Count, StringComparer.Ordinal);
        for (var index = 0; index < keys.Count; index++)
            result[keys[index]] = values[index].IsNullOrEmpty ? 0 : (long)values[index];

        return result;
    }
}
