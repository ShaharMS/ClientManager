using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using ClientManager.DataAccess.Stores.Implementations.Helpers;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using static ClientManager.DataAccess.Stores.Implementations.Helpers.StoreSerialization;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace ClientManager.DataAccess.Stores.Implementations;

/// <summary>
/// Lucene.NET-based implementation of <see cref="IDocumentStore"/>.
/// Uses a single on-disk index directory for all collections. Each document is stored
/// as a Lucene document with a <c>_collection</c> routing field, a <c>_id</c> key field,
/// and the full serialized JSON in a <c>_json</c> stored field. Top-level properties are
/// indexed as typed fields for native search support.
/// Implements <see cref="IDisposable"/> to release the index writer and directory on shutdown.
/// </summary>
public class LuceneDocumentStore : IDocumentStore, IDisposable
{
    private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
    private const string CollectionField = "_collection";
    private const string IdField = "_id";
    private const string JsonField = "_json";
    private const string AllTextField = "_all_text";
    private const string CountersCollection = "_counters";
    private const string CounterCountField = "Count";
    private const string CounterWindowStartField = "WindowStart";
    private const int MaxIdsPerBatch = 512;

    private readonly Lucene.Net.Store.Directory _directory;
    private readonly IndexWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SearcherManager _searcherManager;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="LuceneDocumentStore"/>.
    /// </summary>
    public LuceneDocumentStore() : this(new RAMDirectory())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LuceneDocumentStore"/> using an on-disk index directory.
    /// </summary>
    /// <param name="indexDirectory">The directory where Lucene index files are stored.</param>
    public LuceneDocumentStore(string indexDirectory) : this(FSDirectory.Open(new DirectoryInfo(indexDirectory)))
    {
    }

    private LuceneDocumentStore(Lucene.Net.Store.Directory directory)
    {
        _directory = directory;

        var analyzer = new StandardAnalyzer(AppLuceneVersion);
        var config = new IndexWriterConfig(AppLuceneVersion, analyzer)
        {
            OpenMode = OpenMode.CREATE_OR_APPEND
        };
        _writer = new IndexWriter(_directory, config);
        _searcherManager = new SearcherManager(_writer, applyAllDeletes: true, null);
    }

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class
    {
        var query = new BooleanQuery
        {
            { new TermQuery(new Term(CollectionField, collection)), Occur.MUST },
            { new TermQuery(new Term(IdField, id)), Occur.MUST }
        };

        _searcherManager.MaybeRefreshBlocking();
        var searcher = _searcherManager.Acquire();
        try
        {
            var topDocs = searcher.Search(query, 1);
            if (topDocs.TotalHits == 0)
                return Task.FromResult<T?>(null);

            var doc = searcher.Doc(topDocs.ScoreDocs[0].Doc);
            var json = doc.Get(JsonField);
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, JsonOptions));
        }
        finally
        {
            _searcherManager.Release(searcher);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> GetManyAsync<T>(
        string collection,
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) where T : class
    {
        var requestedIds = ids.Distinct(StringComparer.Ordinal).ToArray();
        if (requestedIds.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
        }

        var results = new List<T>(requestedIds.Length);

        foreach (var idChunk in requestedIds.Chunk(MaxIdsPerBatch))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var query = BuildCollectionIdsQuery(collection, idChunk);
            _searcherManager.MaybeRefreshBlocking();
            var searcher = _searcherManager.Acquire();
            try
            {
                var topDocs = searcher.Search(query, idChunk.Length);
                foreach (var scoreDoc in topDocs.ScoreDocs)
                {
                    var item = DeserializeSearchHit<T>(searcher, scoreDoc.Doc);
                    if (item is not null)
                    {
                        results.Add(item);
                    }
                }
            }
            finally
            {
                _searcherManager.Release(searcher);
            }
        }

        return Task.FromResult<IReadOnlyList<T>>(results);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class
    {
        var query = new TermQuery(new Term(CollectionField, collection));

        _searcherManager.MaybeRefreshBlocking();
        var searcher = _searcherManager.Acquire();
        try
        {
            var topDocs = searcher.Search(query, int.MaxValue);
            var results = new List<T>(topDocs.TotalHits);

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var doc = searcher.Doc(scoreDoc.Doc);
                var json = doc.Get(JsonField);
                var item = JsonSerializer.Deserialize<T>(json, JsonOptions);
                if (item is not null)
                    results.Add(item);
            }

            return Task.FromResult<IReadOnlyList<T>>(results);
        }
        finally
        {
            _searcherManager.Release(searcher);
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class
    {
        await WaitForWriteLockAsync(cancellationToken);
        try
        {
            WriteDocument(collection, id, document);
            _writer.Commit();
        }
        finally
        {
            _writeLock.Release();
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

        await WaitForWriteLockAsync(cancellationToken);
        try
        {
            foreach (var (id, document) in documents)
                WriteDocument(collection, id, document);

            _writer.Commit();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        await WaitForWriteLockAsync(cancellationToken);
        try
        {
            DeleteByCollectionAndId(collection, id);
            _writer.Commit();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<bool> SetIfFieldEqualsAsync<T>(
        string collection,
        string id,
        T document,
        string fieldName,
        object? expectedValue,
        CancellationToken cancellationToken = default) where T : class =>
        DocumentStoreConcurrencyDefaults.SetIfFieldEqualsAsync(
            GetAsync<T>,
            SetAsync,
            collection,
            id,
            document,
            fieldName,
            expectedValue,
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryIncrementWithinLimitsAsync(
        IReadOnlyList<(string key, long max, TimeSpan window)> counters,
        CancellationToken cancellationToken = default) =>
        DocumentStoreConcurrencyDefaults.TryIncrementWithinLimitsAsync(
            IncrementCounterAsync,
            DecrementManyCountersAsync,
            counters,
            cancellationToken);

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
        DocumentStoreConcurrencyDefaults.TryConsumeTokenBucketAsync(
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
    public Task<bool> TryAcquireLeaseAsync(
        string key,
        string ownerId,
        TimeSpan duration,
        CancellationToken cancellationToken = default) =>
        DocumentStoreLeaseDefaults.TryAcquireLeaseAsync(
            GetAsync<LeaseRecord>,
            SetAsync,
            key,
            ownerId,
            duration,
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> RenewLeaseAsync(
        string key,
        string ownerId,
        TimeSpan duration,
        CancellationToken cancellationToken = default) =>
        DocumentStoreLeaseDefaults.RenewLeaseAsync(
            GetAsync<LeaseRecord>,
            SetAsync,
            key,
            ownerId,
            duration,
            cancellationToken);

    /// <inheritdoc />
    public Task ReleaseLeaseAsync(string key, string ownerId, CancellationToken cancellationToken = default) =>
        DocumentStoreLeaseDefaults.ReleaseLeaseAsync(
            GetAsync<LeaseRecord>,
            DeleteAsync,
            key,
            ownerId,
            cancellationToken);

    /// <inheritdoc />
    public async Task<SearchResult<T>> SearchAsync<T>(
        string collection, DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
    {
        var all = await GetAllAsync<T>(collection, cancellationToken);
        return InMemoryQueryEvaluator.Apply(all, query);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync<T>(
        string collection, DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
    {
        var result = await SearchAsync<T>(collection, query, cancellationToken);
        return result.TotalCount;
    }

    /// <inheritdoc />
    public async Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await WaitForWriteLockAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var existing = GetCounterDocument(key);

            long count;
            if (existing is not null)
            {
                var windowStart = new DateTime(existing.GetField(CounterWindowStartField).GetInt64Value()!.Value, DateTimeKind.Utc);
                if (now - windowStart >= window)
                {
                    count = 1;
                }
                else
                {
                    count = existing.GetField(CounterCountField).GetInt64Value()!.Value + 1;
                }
            }
            else
            {
                count = 1;
            }

            WriteCounterDocument(key, count, now);
            _writer.Commit();
            return count;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        await WaitForWriteLockAsync(cancellationToken);
        try
        {
            var existing = GetCounterDocument(key);
            if (existing is null)
                return 0;

            var count = existing.GetField(CounterCountField).GetInt64Value()!.Value;
            if (count <= 0)
                return 0;

            count--;
            var windowStart = new DateTime(existing.GetField(CounterWindowStartField).GetInt64Value()!.Value, DateTimeKind.Utc);
            WriteCounterDocument(key, count, windowStart);
            _writer.Commit();
            return count;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = GetCounterValues([key]);
        return Task.FromResult(result[key]);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, long>> GetManyCountersAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var requestedKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyDictionary<string, long>>(GetCounterValues(requestedKeys));
    }

    /// <inheritdoc />
    public async Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await WaitForWriteLockAsync(cancellationToken);
        try
        {
            WriteCounterDocument(key, value, DateTime.UtcNow);
            _writer.Commit();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetManyCountersAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return;

        await WaitForWriteLockAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            foreach (var (key, (value, _)) in entries)
                WriteCounterDocument(key, value, now);

            _writer.Commit();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> IncrementManyCountersAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return new Dictionary<string, long>();

        await WaitForWriteLockAsync(cancellationToken);
        try
        {
            return IncrementCountersUnderLock(entries);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> DecrementManyCountersAsync(
        IReadOnlyDictionary<string, long> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return new Dictionary<string, long>();

        await WaitForWriteLockAsync(cancellationToken);
        try
        {
            return DecrementCountersUnderLock(entries);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        await WaitForWriteLockAsync(cancellationToken);
        try
        {
            DeleteByCollectionAndId(CountersCollection, key);
            _writer.Commit();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _searcherManager.Dispose();
        _writer.Dispose();
        _directory.Dispose();
        _writeLock.Dispose();
    }

    private void DeleteByCollectionAndId(string collection, string id)
    {
        var deleteQuery = new BooleanQuery
        {
            { new TermQuery(new Term(CollectionField, collection)), Occur.MUST },
            { new TermQuery(new Term(IdField, id)), Occur.MUST }
        };
        _writer.DeleteDocuments(deleteQuery);
    }

    private void WriteDocument<T>(string collection, string id, T document)
    {
        DeleteByCollectionAndId(collection, id);
        _writer.AddDocument(BuildDocument(collection, id, document));
    }

    private async Task WaitForWriteLockAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await _writeLock.WaitAsync(cancellationToken);
        stopwatch.Stop();
        Activity.Current?.SetTag("storage.lock_wait_ms", stopwatch.Elapsed.TotalMilliseconds);
    }

    private Document BuildDocument<T>(string collection, string id, T document)
    {
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var luceneDoc = new Document
        {
            new StringField(CollectionField, collection, Field.Store.YES),
            new StringField(IdField, id, Field.Store.YES),
            new StoredField(JsonField, json)
        };

        var allText = new List<string>();
        var jsonElement = JsonSerializer.SerializeToElement(document, JsonOptions);

        if (jsonElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in jsonElement.EnumerateObject())
            {
                IndexProperty(luceneDoc, property, allText);
            }
        }

        if (allText.Count > 0)
        {
            luceneDoc.Add(new TextField(AllTextField, string.Join(" ", allText), Field.Store.NO));
        }

        return luceneDoc;
    }

    private static void IndexProperty(Document doc, JsonProperty property, List<string> allText)
    {
        var name = property.Name;
        var value = property.Value;

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var str = value.GetString()!;
                doc.Add(new StringField(name, str, Field.Store.NO));
                doc.Add(new TextField($"{name}_text", str, Field.Store.NO));
                allText.Add(str);
                break;

            case JsonValueKind.Number:
                if (value.TryGetInt64(out var longVal))
                    doc.Add(new Int64Field(name, longVal, Field.Store.NO));
                else if (value.TryGetDouble(out var doubleVal))
                    doc.Add(new DoubleField(name, doubleVal, Field.Store.NO));
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                doc.Add(new StringField(name, value.GetBoolean().ToString(CultureInfo.InvariantCulture).ToLowerInvariant(), Field.Store.NO));
                break;
        }
    }

    private Document? GetCounterDocument(string key)
    {
        _searcherManager.MaybeRefreshBlocking();
        var searcher = _searcherManager.Acquire();
        try
        {
            return GetCounterDocument(key, searcher);
        }
        finally
        {
            _searcherManager.Release(searcher);
        }
    }

    private static Document? GetCounterDocument(string key, IndexSearcher searcher)
    {
        var query = new BooleanQuery
        {
            { new TermQuery(new Term(CollectionField, CountersCollection)), Occur.MUST },
            { new TermQuery(new Term(IdField, key)), Occur.MUST }
        };

        var topDocs = searcher.Search(query, 1);
        if (topDocs.TotalHits == 0)
            return null;

        return searcher.Doc(topDocs.ScoreDocs[0].Doc);
    }

    private static Query BuildCollectionIdsQuery(string collection, IReadOnlyCollection<string> ids)
    {
        var idQuery = new BooleanQuery();
        foreach (var requestedId in ids)
        {
            idQuery.Add(new TermQuery(new Term(IdField, requestedId)), Occur.SHOULD);
        }

        return new BooleanQuery
        {
            { new TermQuery(new Term(CollectionField, collection)), Occur.MUST },
            { idQuery, Occur.MUST }
        };
    }

    private static T? DeserializeSearchHit<T>(IndexSearcher searcher, int documentId) where T : class
    {
        var doc = searcher.Doc(documentId);
        var json = doc.Get(JsonField);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private Dictionary<string, long> GetCounterValues(IReadOnlyCollection<string> keys)
    {
        var result = keys.ToDictionary(key => key, _ => 0L, StringComparer.Ordinal);
        if (keys.Count == 0)
            return result;

        foreach (var (key, document) in GetCounterDocuments(keys))
            result[key] = GetCounterCount(document);

        return result;
    }

    private Dictionary<string, Document> GetCounterDocuments(IReadOnlyCollection<string> keys)
    {
        _searcherManager.MaybeRefreshBlocking();
        var searcher = _searcherManager.Acquire();
        try
        {
            return SearchCounterDocuments(keys, searcher);
        }
        finally
        {
            _searcherManager.Release(searcher);
        }
    }

    private static Dictionary<string, Document> SearchCounterDocuments(
        IReadOnlyCollection<string> keys,
        IndexSearcher searcher)
    {
        var result = new Dictionary<string, Document>(StringComparer.Ordinal);
        foreach (var keyChunk in keys.Chunk(MaxIdsPerBatch))
        {
            var topDocs = searcher.Search(BuildCollectionIdsQuery(CountersCollection, keyChunk), keyChunk.Length);
            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var document = searcher.Doc(scoreDoc.Doc);
                result[document.Get(IdField)] = document;
            }
        }

        return result;
    }

    private IReadOnlyDictionary<string, long> IncrementCountersUnderLock(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries)
    {
        var existing = GetCounterDocuments(entries.Keys.ToArray());
        var result = new Dictionary<string, long>(entries.Count, StringComparer.Ordinal);
        var now = DateTime.UtcNow;

        foreach (var (key, (amount, window)) in entries)
            result[key] = IncrementCounterDocument(existing, key, amount, window, now);

        _writer.Commit();
        return result;
    }

    private long IncrementCounterDocument(
        IReadOnlyDictionary<string, Document> existing,
        string key,
        long amount,
        TimeSpan window,
        DateTime now)
    {
        if (amount <= 0)
            return existing.TryGetValue(key, out var current) ? GetCounterCount(current) : 0;

        var count = GetIncrementedCount(existing, key, amount, window, now);
        WriteCounterDocument(key, count, now);
        return count;
    }

    private static long GetIncrementedCount(
        IReadOnlyDictionary<string, Document> existing,
        string key,
        long amount,
        TimeSpan window,
        DateTime now)
    {
        if (!existing.TryGetValue(key, out var document))
            return amount;

        var windowStart = GetCounterWindowStart(document);
        return now - windowStart >= window ? amount : GetCounterCount(document) + amount;
    }

    private IReadOnlyDictionary<string, long> DecrementCountersUnderLock(IReadOnlyDictionary<string, long> entries)
    {
        var existing = GetCounterDocuments(entries.Keys.ToArray());
        var result = new Dictionary<string, long>(entries.Count, StringComparer.Ordinal);
        var changed = false;

        foreach (var (key, amount) in entries)
        {
            result[key] = DecrementCounterDocument(existing, key, amount, out var keyChanged);
            changed = changed || keyChanged;
        }

        if (changed)
            _writer.Commit();

        return result;
    }

    private long DecrementCounterDocument(
        IReadOnlyDictionary<string, Document> existing,
        string key,
        long amount,
        out bool changed)
    {
        changed = false;
        if (amount <= 0 || !existing.TryGetValue(key, out var document))
            return existing.TryGetValue(key, out var current) ? GetCounterCount(current) : 0;

        var count = Math.Max(0, GetCounterCount(document) - amount);
        WriteCounterDocument(key, count, GetCounterWindowStart(document));
        changed = true;
        return count;
    }

    private static long GetCounterCount(Document document) =>
        document.GetField(CounterCountField).GetInt64Value()!.Value;

    private static DateTime GetCounterWindowStart(Document document) =>
        new(document.GetField(CounterWindowStartField).GetInt64Value()!.Value, DateTimeKind.Utc);

    private void WriteCounterDocument(string key, long count, DateTime windowStart)
    {
        DeleteByCollectionAndId(CountersCollection, key);

        var doc = new Document
        {
            new StringField(CollectionField, CountersCollection, Field.Store.YES),
            new StringField(IdField, key, Field.Store.YES),
            new Int64Field(CounterCountField, count, Field.Store.YES),
            new Int64Field(CounterWindowStartField, windowStart.Ticks, Field.Store.YES)
        };

        _writer.AddDocument(doc);
    }
}
