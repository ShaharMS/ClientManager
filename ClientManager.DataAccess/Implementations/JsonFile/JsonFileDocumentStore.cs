using System.Text.Json;
using ClientManager.DataAccess.Interfaces;

namespace ClientManager.DataAccess.Implementations.JsonFile;

/// <summary>
/// JSON file-based implementation of <see cref="IDocumentStore"/>.
/// Stores each collection as a separate JSON file and counters in a dedicated file.
/// Intended for local development and single-instance deployments.
/// </summary>
public class JsonFileDocumentStore : IDocumentStore
{
    private readonly string _dataDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);
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
    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadCollectionAsync<T>(collection, cancellationToken);
            return dict.GetValueOrDefault(id);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadCollectionAsync<T>(collection, cancellationToken);
            return dict.Values.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadCollectionAsync<T>(collection, cancellationToken);
            dict[id] = document;
            await SaveCollectionAsync(collection, dict, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadCollectionAsync<object>(collection, cancellationToken);
            dict.Remove(id);
            await SaveCollectionAsync(collection, dict, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var counters = await LoadCountersAsync(cancellationToken);
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
            await SaveCountersAsync(counters, cancellationToken);
            return entry.Count;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var counters = await LoadCountersAsync(cancellationToken);
            return counters.TryGetValue(key, out var entry) ? entry.Count : 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var counters = await LoadCountersAsync(cancellationToken);
            counters.Remove(key);
            await SaveCountersAsync(counters, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, T>> LoadCollectionAsync<T>(string collection, CancellationToken cancellationToken)
    {
        var path = CollectionPath(collection);
        if (!File.Exists(path))
            return new Dictionary<string, T>();

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<Dictionary<string, T>>(json, JsonOptions)
               ?? new Dictionary<string, T>();
    }

    private async Task SaveCollectionAsync<T>(string collection, Dictionary<string, T> dict, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(dict, JsonOptions);
        await File.WriteAllTextAsync(CollectionPath(collection), json, cancellationToken);
    }

    private async Task<Dictionary<string, CounterEntry>> LoadCountersAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(CounterPath))
            return new Dictionary<string, CounterEntry>();

        var json = await File.ReadAllTextAsync(CounterPath, cancellationToken);
        return JsonSerializer.Deserialize<Dictionary<string, CounterEntry>>(json, JsonOptions)
               ?? new Dictionary<string, CounterEntry>();
    }

    private async Task SaveCountersAsync(Dictionary<string, CounterEntry> counters, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(counters, JsonOptions);
        await File.WriteAllTextAsync(CounterPath, json, cancellationToken);
    }

    private record CounterEntry(long Count, DateTime WindowStart);
}
