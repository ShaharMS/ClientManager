using System.Collections.Concurrent;
using System.Text.Json;
using ClientManager.DataAccess.Stores.Interfaces;

namespace ClientManager.DataAccess.Stores.Implementations;

/// <summary>
/// JSON file-based implementation of <see cref="IDocumentStore"/>.
/// Stores each collection as a separate JSON file and counters in a dedicated file.
/// Uses an in-memory write-through cache so reads never hit disk after first load.
/// Intended for local development and single-instance deployments.
/// </summary>
public class JsonFileDocumentStore : IDocumentStore
{
    private readonly string _dataDirectory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, JsonElement>> _collectionCache = new();
    private ConcurrentDictionary<string, CounterEntry>? _counterCache;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="JsonFileDocumentStore"/>.
    /// </summary>
    /// <param name="dataDirectory">The directory where JSON data files are stored.</param>
    public JsonFileDocumentStore(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
        Directory.CreateDirectory(dataDirectory);
    }

    private string CollectionPath(string collection) => Path.Combine(_dataDirectory, $"{collection}.json");
    private string CounterPath => Path.Combine(_dataDirectory, "_counters.json");

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

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var dict = GetOrLoadCollection(collection);
            dict[id] = element;
            await PersistCollectionAsync(collection, dict, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var dict = GetOrLoadCollection(collection);
            dict.TryRemove(id, out _);
            await PersistCollectionAsync(collection, dict, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var counters = GetOrLoadCounters();
            var now = DateTime.UtcNow;

            if (counters.TryGetValue(key, out var entry) && now - entry.WindowStart < window)
            {
                entry = entry with { Count = entry.Count + 1 };
            }
            else
            {
                entry = new CounterEntry(1, now);
            }

            counters[key] = entry;
            await PersistCountersAsync(counters, cancellationToken);
            return entry.Count;
        }
        finally
        {
            _writeLock.Release();
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
    public async Task ResetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var counters = GetOrLoadCounters();
            counters.TryRemove(key, out _);
            await PersistCountersAsync(counters, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var counters = GetOrLoadCounters();
            counters[key] = new CounterEntry(value, DateTime.UtcNow);
            await PersistCountersAsync(counters, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private ConcurrentDictionary<string, JsonElement> GetOrLoadCollection(string collection)
    {
        return _collectionCache.GetOrAdd(collection, key =>
        {
            var path = CollectionPath(key);
            if (!File.Exists(path))
                return new ConcurrentDictionary<string, JsonElement>();

            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions) ?? [];
            return new ConcurrentDictionary<string, JsonElement>(dict);
        });
    }

    private ConcurrentDictionary<string, CounterEntry> GetOrLoadCounters()
    {
        if (_counterCache is not null)
            return _counterCache;

        if (!File.Exists(CounterPath))
        {
            _counterCache = new ConcurrentDictionary<string, CounterEntry>();
            return _counterCache;
        }

        try
        {
            var json = File.ReadAllText(CounterPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, CounterEntry>>(json, JsonOptions) ?? [];
            _counterCache = new ConcurrentDictionary<string, CounterEntry>(dict);
        }
        catch (JsonException)
        {
            _counterCache = new ConcurrentDictionary<string, CounterEntry>();
        }

        return _counterCache;
    }

    private async Task PersistCollectionAsync(string collection, ConcurrentDictionary<string, JsonElement> dict, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(dict, JsonOptions);
        await AtomicWriteAsync(CollectionPath(collection), json, cancellationToken);
    }

    private async Task PersistCountersAsync(ConcurrentDictionary<string, CounterEntry> counters, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(counters, JsonOptions);
        await AtomicWriteAsync(CounterPath, json, cancellationToken);
    }

    private static async Task AtomicWriteAsync(string targetPath, string content, CancellationToken cancellationToken)
    {
        var tmpPath = targetPath + ".tmp";
        await File.WriteAllTextAsync(tmpPath, content, cancellationToken);
        File.Move(tmpPath, targetPath, overwrite: true);
    }

    private record CounterEntry(long Count, DateTime WindowStart);
}
