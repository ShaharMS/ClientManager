using System.Globalization;
using System.Text.Json;
using ClientManager.DataAccess.Stores.Implementations.Helpers;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
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

    private readonly Lucene.Net.Store.Directory _directory;
    private readonly IndexWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SearcherManager _searcherManager;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="LuceneDocumentStore"/>.
    /// </summary>
    /// <param name="indexDirectory">The directory path for Lucene index files (unused — index is kept in memory).</param>
    public LuceneDocumentStore(string indexDirectory)
    {
        _directory = new RAMDirectory();

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
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            DeleteByCollectionAndId(collection, id);
            var luceneDoc = BuildDocument(collection, id, document);
            _writer.AddDocument(luceneDoc);
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
        await _writeLock.WaitAsync(cancellationToken);
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
        await _writeLock.WaitAsync(cancellationToken);
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

            WriteCounter(key, count, now);
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
        await _writeLock.WaitAsync(cancellationToken);
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
            WriteCounter(key, count, windowStart);
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
        _searcherManager.MaybeRefreshBlocking();
        var searcher = _searcherManager.Acquire();
        try
        {
            var doc = GetCounterDocument(key, searcher);
            if (doc is null)
                return Task.FromResult(0L);

            return Task.FromResult(doc.GetField(CounterCountField).GetInt64Value()!.Value);
        }
        finally
        {
            _searcherManager.Release(searcher);
        }
    }

    /// <inheritdoc />
    public async Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            WriteCounter(key, value, DateTime.UtcNow);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
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

    private void WriteCounter(string key, long count, DateTime windowStart)
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
        _writer.Commit();
    }
}
