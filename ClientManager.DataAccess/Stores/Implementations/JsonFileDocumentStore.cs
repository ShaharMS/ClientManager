using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using ClientManager.DataAccess.Stores.Implementations.Helpers;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using static ClientManager.DataAccess.Stores.Implementations.Helpers.StoreSerialization;

namespace ClientManager.DataAccess.Stores.Implementations;

/// <summary>
/// JSON file-based implementation of <see cref="IDocumentStore"/>.
/// Stores each collection as a separate JSON file and counters in a dedicated file.
/// Uses an in-memory write-through cache so reads never hit disk after first load.
/// Intended for local development and single-instance deployments.
/// </summary>
public class JsonFileDocumentStore : IDocumentStore
{
    private const int MaxAtomicWriteAttempts = 6;
    private const int AtomicWriteRetryBaseDelayMilliseconds = 25;
    private const int MtimeReloadDebounceMilliseconds = 400;

    private readonly string _dataDirectory;
    private readonly SharedStoreState _state;
    private static readonly ConcurrentDictionary<string, SharedStoreState> States = new(GetPathComparer());
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TargetLocks = new(GetPathComparer());

    /// <summary>
    /// Initializes a new instance of <see cref="JsonFileDocumentStore"/>.
    /// </summary>
    /// <param name="dataDirectory">The directory where JSON data files are stored.</param>
    public JsonFileDocumentStore(string dataDirectory)
    {
        _dataDirectory = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(_dataDirectory);
        _state = States.GetOrAdd(_dataDirectory, _ => new SharedStoreState());
    }

    private string CollectionPath(string collection) => Path.Combine(_dataDirectory, $"{collection}.json");
    private string CounterPath => Path.Combine(_dataDirectory, "_counters.json");

    private async Task WaitForWriteLockAsync(SemaphoreSlim writeLock, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await writeLock.WaitAsync(cancellationToken);
        stopwatch.Stop();
        Activity.Current?.SetTag("storage.lock_wait_ms", stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class
    {
        var dict = GetOrLoadCollection(collection);
        if (dict.TryGetValue(id, out var element))
        {
            return Task.FromResult(element.Deserialize<T>(JsonOptions));
        }
        return Task.FromResult<T?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> GetManyAsync<T>(
        string collection,
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) where T : class
    {
        var dict = GetOrLoadCollection(collection);
        var requestedIds = ids.Distinct(StringComparer.Ordinal).ToList();
        var results = new List<T>(requestedIds.Count);

        foreach (var requestedId in requestedIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!dict.TryGetValue(requestedId, out var element))
            {
                continue;
            }

            var item = element.Deserialize<T>(JsonOptions);
            if (item is not null)
            {
                results.Add(item);
            }
        }

        return Task.FromResult<IReadOnlyList<T>>(results);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class
    {
        var dict = GetOrLoadCollection(collection);
        var list = new List<T>(dict.Count);
        foreach (var element in dict.Values)
        {
            var item = element.Deserialize<T>(JsonOptions);
            if (item is not null)
                list.Add(item);
        }
        return Task.FromResult<IReadOnlyList<T>>(list);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class
    {
        var element = JsonSerializer.SerializeToElement(document, JsonOptions);
        var writeLock = GetCollectionWriteLock(collection);

        await WaitForWriteLockAsync(writeLock, cancellationToken);
        try
        {
            var dict = GetOrLoadCollection(collection);
            dict[id] = element;
            await PersistCollectionAsync(collection, dict, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetManyAsync<T>(
        string collection,
        IReadOnlyDictionary<string, T> documents,
        CancellationToken cancellationToken = default) where T : class
    {
        if (documents.Count == 0)
            return;

        var elements = SerializeDocuments(documents);
        var writeLock = GetCollectionWriteLock(collection);

        await WaitForWriteLockAsync(writeLock, cancellationToken);
        try
        {
            var dict = GetOrLoadCollection(collection);
            foreach (var (id, element) in elements)
                dict[id] = element;

            await PersistCollectionAsync(collection, dict, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        var writeLock = GetCollectionWriteLock(collection);

        await WaitForWriteLockAsync(writeLock, cancellationToken);
        try
        {
            var dict = GetOrLoadCollection(collection);
            dict.TryRemove(id, out _);
            await PersistCollectionAsync(collection, dict, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<(bool IsAllowed, long RemainingTokens, long RetryAfterSeconds)> TryConsumeTokenBucketAsync(
        string tokensKey,
        string lastRefillKey,
        int bucketCapacity,
        int tokensPerRefill,
        long refillIntervalSeconds,
        TimeSpan stateWindow,
        long nowUnixSeconds,
        CancellationToken cancellationToken = default) =>
        TokenBucketConsumeDefaults.TryConsumeTokenBucketAsync(
            GetManyCountersAsync,
            SetManyCountersAsync,
            tokensKey,
            lastRefillKey,
            bucketCapacity,
            tokensPerRefill,
            refillIntervalSeconds,
            stateWindow,
            nowUnixSeconds,
            cancellationToken);

    /// <inheritdoc />
    public async Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await WaitForWriteLockAsync(_state.CounterWriteLock, cancellationToken);
        try
        {
            return await IncrementCounterUnderLockAsync(key, 1, window, cancellationToken);
        }
        finally
        {
            _state.CounterWriteLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> IncrementManyCountersAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return new Dictionary<string, long>();

        await WaitForWriteLockAsync(_state.CounterWriteLock, cancellationToken);
        try
        {
            return await IncrementCountersUnderLockAsync(entries, cancellationToken);
        }
        finally
        {
            _state.CounterWriteLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var counters = GetOrLoadCounters();
        var result = counters.TryGetValue(key, out var entry) ? entry.Count : 0;
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, long>> GetManyCountersAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var counters = GetOrLoadCounters();
        var requestedKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
        var result = new Dictionary<string, long>(requestedKeys.Length, StringComparer.Ordinal);

        foreach (var requestedKey in requestedKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[requestedKey] = counters.TryGetValue(requestedKey, out var entry) ? entry.Count : 0;
        }

        return Task.FromResult<IReadOnlyDictionary<string, long>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, long>> GetCountersByPrefixAsync(
        string keyPrefix,
        CancellationToken cancellationToken = default)
    {
        var counters = GetOrLoadCounters();
        var result = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var (key, entry) in counters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Count > 0 && key.StartsWith(keyPrefix, StringComparison.Ordinal))
            {
                result[key] = entry.Count;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, long>>(result);
    }

    /// <inheritdoc />
    public async Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        await WaitForWriteLockAsync(_state.CounterWriteLock, cancellationToken);
        try
        {
            return await DecrementCounterUnderLockAsync(key, 1, cancellationToken);
        }
        finally
        {
            _state.CounterWriteLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> DecrementManyCountersAsync(
        IReadOnlyDictionary<string, long> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return new Dictionary<string, long>();

        await WaitForWriteLockAsync(_state.CounterWriteLock, cancellationToken);
        try
        {
            return await DecrementCountersUnderLockAsync(entries, cancellationToken);
        }
        finally
        {
            _state.CounterWriteLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        await ResetManyCountersAsync([key], cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResetManyCountersAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return;
        }

        await WaitForWriteLockAsync(_state.CounterWriteLock, cancellationToken);
        try
        {
            var counters = GetOrLoadCounters();
            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                counters.TryRemove(key, out _);
            }

            await PersistCountersAsync(counters, cancellationToken);
        }
        finally
        {
            _state.CounterWriteLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> PurgeCountersByPrefixAsync(
        string keyPrefix,
        Func<string, long, DateTime?, bool> shouldPurge,
        CancellationToken cancellationToken = default)
    {
        await WaitForWriteLockAsync(_state.CounterWriteLock, cancellationToken);
        try
        {
            var counters = GetOrLoadCounters();
            var keysToRemove = new List<string>();

            foreach (var (key, entry) in counters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!key.StartsWith(keyPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (shouldPurge(key, entry.Count, entry.WindowStart))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                counters.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                await PersistCountersAsync(counters, cancellationToken);
            }

            return keysToRemove.Count;
        }
        finally
        {
            _state.CounterWriteLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await WaitForWriteLockAsync(_state.CounterWriteLock, cancellationToken);
        try
        {
            await SetCountersUnderLockAsync(
                new Dictionary<string, (long value, TimeSpan window)> { [key] = (value, window) },
                cancellationToken);
        }
        finally
        {
            _state.CounterWriteLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetManyCountersAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return;

        await WaitForWriteLockAsync(_state.CounterWriteLock, cancellationToken);
        try
        {
            await SetCountersUnderLockAsync(entries, cancellationToken);
        }
        finally
        {
            _state.CounterWriteLock.Release();
        }
    }

    /// <summary>
    /// Executes a search query against the in-memory collection cache.
    /// <para>
    ///     The JSON file store maintains a full in-memory cache of all documents (loaded on first
    ///     access and kept in sync via write-through). All filtering, sorting, and pagination are
    ///     applied in memory using <see cref="InMemoryQueryEvaluator"/>. This is functionally
    ///     correct but does not scale — for production workloads with large collections, use the
    ///     Lucene, MongoDB, or Redis (with RediSearch) providers which support native server-side
    ///     query execution.
    /// </para>
    /// </summary>
    public async Task<SearchResult<T>> SearchAsync<T>(
        string collection, DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(query.TextSearch) &&
            query.Filters.Count > 0 &&
            query.Filters.All(filter => filter.Operator == FilterOperator.Equals))
        {
            var dict = GetOrLoadCollection(collection);
            var matches = new List<T>(Math.Min(dict.Count, 256));
            foreach (var element in dict.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!query.Filters.All(filter => JsonElementMatchesFilter(element, filter)))
                {
                    continue;
                }

                var item = element.Deserialize<T>(JsonOptions);
                if (item is not null)
                {
                    matches.Add(item);
                }
            }

            return InMemoryQueryEvaluator.Apply(
                matches,
                new DocumentQuery
                {
                    Sort = query.Sort,
                    Skip = query.Skip,
                    Take = query.Take
                });
        }

        var all = await GetAllAsync<T>(collection, cancellationToken);
        return InMemoryQueryEvaluator.Apply(all, query);
    }

    /// <summary>
    /// Counts documents matching the query by delegating to <see cref="SearchAsync{T}"/>
    /// and returning the total. Since the JSON file store is entirely in-memory, there is
    /// no performance benefit to a separate count path.
    /// </summary>
    public async Task<long> CountAsync<T>(
        string collection, DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsCountOnlyQuery(query))
        {
            var dict = GetOrLoadCollection(collection);
            if (query.Filters.Count == 0 && string.IsNullOrEmpty(query.TextSearch))
            {
                return dict.Count;
            }

            if (string.IsNullOrEmpty(query.TextSearch) && query.Filters.Count == 1)
            {
                return CountBySingleJsonFilter(dict, query.Filters[0], cancellationToken);
            }
        }

        var result = await SearchAsync<T>(collection, query, cancellationToken);
        return result.TotalCount;
    }

    private static bool IsCountOnlyQuery(DocumentQuery query) =>
        query.Skip is null or 0 && query.Take is null && query.Sort is null;

    private static long CountBySingleJsonFilter(
        ConcurrentDictionary<string, JsonElement> dict,
        FilterClause filter,
        CancellationToken cancellationToken)
    {
        long count = 0;
        foreach (var element in dict.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (JsonElementMatchesFilter(element, filter))
            {
                count++;
            }
        }

        return count;
    }

    private static bool JsonElementMatchesFilter(JsonElement element, FilterClause filter)
    {
        if (!element.TryGetProperty(filter.FieldName, out var property))
        {
            return false;
        }

        return filter.Operator switch
        {
            FilterOperator.Equals => JsonElementEquals(property, filter.Value),
            _ => false
        };
    }

    private static bool JsonElementEquals(JsonElement property, object value) => value switch
    {
        bool boolValue when property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False
            => property.GetBoolean() == boolValue,
        string stringValue when property.ValueKind == JsonValueKind.String
            => string.Equals(property.GetString(), stringValue, StringComparison.Ordinal),
        int intValue when property.ValueKind == JsonValueKind.Number
            => property.TryGetInt32(out var number) && number == intValue,
        _ => false
    };

    private ConcurrentDictionary<string, JsonElement> GetOrLoadCollection(string collection)
    {
        var path = CollectionPath(collection);
        var fileMtime = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

        if (_state.CollectionLoadTimes.TryGetValue(collection, out var loadedAt)
            && fileMtime > loadedAt
            && ShouldReloadAfterMtimeChange(fileMtime)
            && _state.CollectionCache.TryRemove(collection, out _))
        {
            _state.CollectionLoadTimes.TryRemove(collection, out _);
        }

        return _state.CollectionCache.GetOrAdd(collection, key => LoadCollectionFromDisk(key, path));
    }

    private static bool ShouldReloadAfterMtimeChange(DateTime fileModifiedUtc) =>
        (DateTime.UtcNow - fileModifiedUtc).TotalMilliseconds >= MtimeReloadDebounceMilliseconds;

    private ConcurrentDictionary<string, JsonElement> LoadCollectionFromDisk(string collection, string basePath)
    {
        Dictionary<string, JsonElement> dict;
        if (!File.Exists(basePath))
        {
            dict = [];
        }
        else
        {
            var json = File.ReadAllText(basePath);
            dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions) ?? [];
        }

        var fileMtime = File.Exists(basePath) ? File.GetLastWriteTimeUtc(basePath) : DateTime.MinValue;
        _state.CollectionLoadTimes[collection] = fileMtime;
        return new ConcurrentDictionary<string, JsonElement>(dict);
    }

    private ConcurrentDictionary<string, CounterEntry> GetOrLoadCounters()
    {
        lock (_state.CounterCacheLock)
        {
            if (_state.CounterCache is not null)
                return _state.CounterCache;

            _state.CounterCache = LoadCountersFromDisk();
            return _state.CounterCache;
        }
    }

    private ConcurrentDictionary<string, CounterEntry> LoadCountersFromDisk()
    {
        if (!File.Exists(CounterPath))
            return new ConcurrentDictionary<string, CounterEntry>();

        try
        {
            var json = File.ReadAllText(CounterPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, CounterEntry>>(json, JsonOptions) ?? [];
            return new ConcurrentDictionary<string, CounterEntry>(dict);
        }
        catch (JsonException)
        {
            return new ConcurrentDictionary<string, CounterEntry>();
        }
    }

    private SemaphoreSlim GetCollectionWriteLock(string collection) =>
        _state.CollectionWriteLocks.GetOrAdd(collection, _ => new SemaphoreSlim(1, 1));

    private static Dictionary<string, JsonElement> SerializeDocuments<T>(
        IReadOnlyDictionary<string, T> documents) where T : class
    {
        var elements = new Dictionary<string, JsonElement>(documents.Count, StringComparer.Ordinal);
        foreach (var (id, document) in documents)
            elements[id] = JsonSerializer.SerializeToElement(document, JsonOptions);

        return elements;
    }

    private async Task<long> IncrementCounterUnderLockAsync(
        string key,
        long amount,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var result = await IncrementCountersUnderLockAsync(
            new Dictionary<string, (long amount, TimeSpan window)> { [key] = (amount, window) },
            cancellationToken);
        return result[key];
    }

    private async Task<IReadOnlyDictionary<string, long>> IncrementCountersUnderLockAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken)
    {
        var counters = GetOrLoadCounters();
        var now = DateTime.UtcNow;
        var result = new Dictionary<string, long>(entries.Count, StringComparer.Ordinal);

        foreach (var (key, (amount, window)) in entries)
            result[key] = IncrementCounterValue(counters, key, amount, window, now);

        await PersistCountersAsync(counters, cancellationToken);
        return result;
    }

    private static long IncrementCounterValue(
        ConcurrentDictionary<string, CounterEntry> counters,
        string key,
        long amount,
        TimeSpan window,
        DateTime now)
    {
        if (amount <= 0)
            return counters.TryGetValue(key, out var current) ? current.Count : 0;

        var entry = CreateIncrementedEntry(counters, key, amount, window, now);
        counters[key] = entry;
        return entry.Count;
    }

    private static CounterEntry CreateIncrementedEntry(
        ConcurrentDictionary<string, CounterEntry> counters,
        string key,
        long amount,
        TimeSpan window,
        DateTime now)
    {
        return counters.TryGetValue(key, out var entry) && now - entry.WindowStart < window
            ? entry with { Count = entry.Count + amount }
            : new CounterEntry(amount, now);
    }

    private async Task<long> DecrementCounterUnderLockAsync(
        string key,
        long amount,
        CancellationToken cancellationToken)
    {
        var result = await DecrementCountersUnderLockAsync(
            new Dictionary<string, long> { [key] = amount },
            cancellationToken);
        return result[key];
    }

    private async Task<IReadOnlyDictionary<string, long>> DecrementCountersUnderLockAsync(
        IReadOnlyDictionary<string, long> entries,
        CancellationToken cancellationToken)
    {
        var counters = GetOrLoadCounters();
        var result = new Dictionary<string, long>(entries.Count, StringComparer.Ordinal);
        var changed = false;

        foreach (var (key, amount) in entries)
        {
            var value = DecrementCounterValue(counters, key, amount, out var keyChanged);
            changed = changed || keyChanged;
            result[key] = value;
        }

        if (changed)
            await PersistCountersAsync(counters, cancellationToken);

        return result;
    }

    private static long DecrementCounterValue(
        ConcurrentDictionary<string, CounterEntry> counters,
        string key,
        long amount,
        out bool changed)
    {
        changed = false;
        if (!counters.TryGetValue(key, out var entry))
            return 0;

        if (amount <= 0 || entry.Count <= 0)
            return entry.Count;

        var count = Math.Max(0, entry.Count - amount);
        counters[key] = entry with { Count = count };
        changed = true;
        return count;
    }

    private async Task SetCountersUnderLockAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken)
    {
        var counters = GetOrLoadCounters();
        var now = DateTime.UtcNow;

        foreach (var (key, (value, _)) in entries)
            counters[key] = new CounterEntry(value, now);

        await PersistCountersAsync(counters, cancellationToken);
    }

    private async Task PersistCollectionAsync(string collection, ConcurrentDictionary<string, JsonElement> dict, CancellationToken cancellationToken)
    {
        var path = CollectionPath(collection);
        var json = JsonSerializer.Serialize(dict, JsonOptions);
        await AtomicWriteAsync(path, json, cancellationToken);
        _state.CollectionLoadTimes[collection] = File.GetLastWriteTimeUtc(path);
    }

    private async Task PersistCountersAsync(ConcurrentDictionary<string, CounterEntry> counters, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(counters, JsonOptions);
        await AtomicWriteAsync(CounterPath, json, cancellationToken);
    }

    private static async Task AtomicWriteAsync(string targetPath, string content, CancellationToken cancellationToken)
    {
        var targetLock = TargetLocks.GetOrAdd(Path.GetFullPath(targetPath), _ => new SemaphoreSlim(1, 1));
        await targetLock.WaitAsync(cancellationToken);

        var tmpPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tmpPath, content, cancellationToken);
            await MoveWithRetryAsync(tmpPath, targetPath, cancellationToken);
        }
        finally
        {
            TryDeleteTempFile(tmpPath);
            targetLock.Release();
        }
    }

    private static async Task MoveWithRetryAsync(string tmpPath, string targetPath, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(tmpPath, targetPath, overwrite: true);
                return;
            }
            catch (Exception exception) when (CanRetryAtomicMove(exception, attempt))
            {
                var delay = TimeSpan.FromMilliseconds(AtomicWriteRetryBaseDelayMilliseconds * attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool CanRetryAtomicMove(Exception exception, int attempt) =>
        attempt < MaxAtomicWriteAttempts &&
        (exception is IOException || exception is UnauthorizedAccessException);

    private static void TryDeleteTempFile(string tmpPath)
    {
        try
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private sealed class SharedStoreState
    {
        public SemaphoreSlim CounterWriteLock { get; } = new(1, 1);
        public object CounterCacheLock { get; } = new();
        public ConcurrentDictionary<string, ConcurrentDictionary<string, JsonElement>> CollectionCache { get; } = new();
        public ConcurrentDictionary<string, DateTime> CollectionLoadTimes { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, SemaphoreSlim> CollectionWriteLocks { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, CounterEntry>? CounterCache { get; set; }
    }

    private record CounterEntry(long Count, DateTime WindowStart);
}
