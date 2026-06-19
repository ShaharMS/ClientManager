using System.Text.Json;
using System.Text.RegularExpressions;
using ClientManager.DataAccess.Stores.Implementations.Helpers;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using static ClientManager.DataAccess.Stores.Implementations.Helpers.StoreSerialization;
using SearchDirection = ClientManager.Shared.Models.Search.SortDirection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace ClientManager.DataAccess.Stores.Implementations;

/// <summary>
/// MongoDB-based implementation of <see cref="IDocumentStore"/>.
/// Each collection name maps to a MongoDB collection. Counters use a dedicated collection.
/// </summary>
/// <param name="database">The MongoDB database instance to use.</param>
public class MongoDBDocumentStore(IMongoDatabase database) : IDocumentStore
{
    private IMongoCollection<BsonDocument> GetCollection(string collection) =>
        database.GetCollection<BsonDocument>(collection);

    private IMongoCollection<BsonDocument> CounterCollection =>
        database.GetCollection<BsonDocument>("_counters");

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        var doc = await GetCollection(collection).Find(filter).FirstOrDefaultAsync(cancellationToken);
        return doc is null ? null : DeserializeDocument<T>(doc);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetManyAsync<T>(
        string collection,
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) where T : class
    {
        var requestedIds = ids.Distinct(StringComparer.Ordinal).ToList();
        if (requestedIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<BsonDocument>.Filter.In("_id", requestedIds);
        var docs = await GetCollection(collection).Find(filter).ToListAsync(cancellationToken);
        return docs.Select(DeserializeDocument<T>).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class
    {
        var docs = await GetCollection(collection).Find(Builders<BsonDocument>.Filter.Empty).ToListAsync(cancellationToken);
        return docs.Select(DeserializeDocument<T>).ToList();
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        var bsonDoc = CreateDocument(id, document);
        await GetCollection(collection).ReplaceOneAsync(filter, bsonDoc, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetManyAsync<T>(
        string collection,
        IReadOnlyDictionary<string, T> documents,
        CancellationToken cancellationToken = default) where T : class
    {
        if (documents.Count == 0)
            return;

        var models = documents.Select(entry =>
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", entry.Key);
            return new ReplaceOneModel<BsonDocument>(filter, CreateDocument(entry.Key, entry.Value))
            {
                IsUpsert = true
            };
        }).ToList<WriteModel<BsonDocument>>();

        await GetCollection(collection).BulkWriteAsync(models, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        await GetCollection(collection).DeleteOneAsync(filter, cancellationToken);
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
        return await IncrementCounterByAsync(key, 1, window, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> IncrementManyCountersAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return new Dictionary<string, long>();

        var tasks = entries.ToDictionary(
            entry => entry.Key,
            entry => IncrementCounterByAsync(entry.Key, entry.Value.amount, entry.Value.window, cancellationToken),
            StringComparer.Ordinal);

        return await CompleteCounterTasksAsync(tasks);
    }

    /// <inheritdoc />
    public async Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
        var doc = await CounterCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return doc?["Count"].AsInt64 ?? 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> GetManyCountersAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var requestedKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
        var existing = await LoadCounterDocumentsAsync(requestedKeys, cancellationToken);
        var result = new Dictionary<string, long>(requestedKeys.Length, StringComparer.Ordinal);

        foreach (var requestedKey in requestedKeys)
            result[requestedKey] = existing.TryGetValue(requestedKey, out var document) ? GetCounterCount(document) : 0;

        return result;
    }

    /// <inheritdoc />
    public async Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        return await DecrementCounterByAsync(key, 1, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> DecrementManyCountersAsync(
        IReadOnlyDictionary<string, long> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return new Dictionary<string, long>();

        var tasks = entries.ToDictionary(
            entry => entry.Key,
            entry => DecrementCounterByAsync(entry.Key, entry.Value, cancellationToken),
            StringComparer.Ordinal);

        return await CompleteCounterTasksAsync(tasks);
    }

    /// <inheritdoc />
    public async Task ResetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
        await CounterCollection.DeleteOneAsync(filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
        var doc = new BsonDocument
        {
            { "_id", key },
            { "Count", value },
            { "WindowStart", DateTime.UtcNow }
        };
        await CounterCollection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetManyCountersAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var models = entries.Select(entry =>
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", entry.Key);
            return new ReplaceOneModel<BsonDocument>(filter, CreateCounterDocument(entry.Key, entry.Value.value, now))
            {
                IsUpsert = true
            };
        }).ToList<WriteModel<BsonDocument>>();

        await BulkWriteCountersAsync(models, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SearchResult<T>> SearchAsync<T>(
        string collection, DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
    {
        var mongoCollection = GetCollection(collection);
        var filter = BuildFilter(query);

        var totalCount = await mongoCollection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var findFluent = mongoCollection.Find(filter);

        if (query.Sort is not null)
        {
            var sort = query.Sort.Direction == SearchDirection.Ascending
                ? Builders<BsonDocument>.Sort.Ascending(query.Sort.FieldName)
                : Builders<BsonDocument>.Sort.Descending(query.Sort.FieldName);
            findFluent = findFluent.Sort(sort);
        }

        if (query.Skip.HasValue)
            findFluent = findFluent.Skip(query.Skip.Value);

        if (query.Take.HasValue)
            findFluent = findFluent.Limit(query.Take.Value);

        var docs = await findFluent.ToListAsync(cancellationToken);
        var items = docs.Select(DeserializeDocument<T>).ToList();

        return new SearchResult<T>(items, totalCount);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync<T>(
        string collection, DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
    {
        var filter = BuildFilter(query);
        return await GetCollection(collection).CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    private static FilterDefinition<BsonDocument> BuildFilter(DocumentQuery query)
    {
        var filters = new List<FilterDefinition<BsonDocument>>();

        foreach (var clause in query.Filters)
        {
            filters.Add(TranslateFilter(clause));
        }

        if (!string.IsNullOrEmpty(query.TextSearch))
        {
            var escaped = Regex.Escape(query.TextSearch);
            var regex = new BsonRegularExpression(escaped, "i");
            filters.Add(Builders<BsonDocument>.Filter.Regex("$**", regex));
        }

        return filters.Count == 0
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.And(filters);
    }

    private static FilterDefinition<BsonDocument> TranslateFilter(FilterClause clause)
    {
        var field = clause.FieldName;
        var value = BsonValue.Create(clause.Value);

        return clause.Operator switch
        {
            FilterOperator.Equals => Builders<BsonDocument>.Filter.Eq(field, value),
            FilterOperator.NotEquals => Builders<BsonDocument>.Filter.Ne(field, value),
            FilterOperator.Contains => Builders<BsonDocument>.Filter.Regex(
                field, new BsonRegularExpression(Regex.Escape(clause.Value.ToString()!), "i")),
            FilterOperator.StartsWith => Builders<BsonDocument>.Filter.Regex(
                field, new BsonRegularExpression("^" + Regex.Escape(clause.Value.ToString()!), "i")),
            FilterOperator.GreaterThan => Builders<BsonDocument>.Filter.Gt(field, value),
            FilterOperator.GreaterThanOrEqual => Builders<BsonDocument>.Filter.Gte(field, value),
            FilterOperator.LessThan => Builders<BsonDocument>.Filter.Lt(field, value),
            FilterOperator.LessThanOrEqual => Builders<BsonDocument>.Filter.Lte(field, value),
            _ => Builders<BsonDocument>.Filter.Empty
        };
    }

    private static T DeserializeDocument<T>(BsonDocument doc)
    {
        doc.Remove("_id");
        var json = doc.ToJson();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
    }

    private static BsonDocument CreateDocument<T>(string id, T document)
    {
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var bsonDoc = BsonDocument.Parse(json);
        bsonDoc["_id"] = id;
        return bsonDoc;
    }

    private async Task<Dictionary<string, BsonDocument>> LoadCounterDocumentsAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken)
    {
        var requestedKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
        if (requestedKeys.Length == 0)
            return new Dictionary<string, BsonDocument>(StringComparer.Ordinal);

        var filter = Builders<BsonDocument>.Filter.In("_id", requestedKeys);
        var docs = await CounterCollection.Find(filter).ToListAsync(cancellationToken);
        return docs.ToDictionary(document => document["_id"].AsString, StringComparer.Ordinal);
    }

    private async Task<long> IncrementCounterByAsync(
        string key,
        long amount,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
            return await GetCounterAsync(key, cancellationToken);

        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
        var update = CreateIncrementPipelineUpdate(amount, window, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var result = await CounterCollection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        return result is null ? 0 : GetCounterCount(result);
    }

    private async Task<long> DecrementCounterByAsync(
        string key,
        long amount,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
            return await GetCounterAsync(key, cancellationToken);

        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After
        };

        var update = CreateDecrementPipelineUpdate(amount);
        var result = await CounterCollection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        return result is null ? 0 : GetCounterCount(result);
    }

    private static async Task<IReadOnlyDictionary<string, long>> CompleteCounterTasksAsync(
        IReadOnlyDictionary<string, Task<long>> tasks)
    {
        var result = new Dictionary<string, long>(tasks.Count, StringComparer.Ordinal);
        foreach (var (key, task) in tasks)
            result[key] = await task;

        return result;
    }

    private static UpdateDefinition<BsonDocument> CreateIncrementPipelineUpdate(long amount, TimeSpan window, DateTime now)
    {
        var stage = new BsonDocument("$set", new BsonDocument
        {
            { "Count", CreateIncrementCountExpression(amount, window, now) },
            { "WindowStart", CreateIncrementWindowStartExpression(window, now) }
        });

        var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(new[] { stage });
        return Builders<BsonDocument>.Update.Pipeline(pipeline);
    }

    private static BsonDocument CreateIncrementCountExpression(long amount, TimeSpan window, DateTime now)
    {
        var currentCount = new BsonDocument("$ifNull", new BsonArray { "$Count", 0L });
        var incrementedCount = new BsonDocument("$add", new BsonArray { currentCount, amount });

        return new BsonDocument("$cond", new BsonArray
        {
            CreateCounterExpiredExpression(window, now),
            amount,
            incrementedCount
        });
    }

    private static BsonDocument CreateIncrementWindowStartExpression(TimeSpan window, DateTime now)
    {
        return new BsonDocument("$cond", new BsonArray
        {
            CreateCounterExpiredExpression(window, now),
            new BsonDateTime(now),
            "$WindowStart"
        });
    }

    private static BsonDocument CreateCounterExpiredExpression(TimeSpan window, DateTime now)
    {
        var elapsedMilliseconds = new BsonDocument("$subtract", new BsonArray { new BsonDateTime(now), "$WindowStart" });
        return new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { "$WindowStart", BsonNull.Value }),
            new BsonDocument("$gte", new BsonArray { elapsedMilliseconds, (long)window.TotalMilliseconds })
        });
    }

    private static UpdateDefinition<BsonDocument> CreateDecrementPipelineUpdate(long amount)
    {
        var currentCount = new BsonDocument("$ifNull", new BsonArray { "$Count", 0L });
        var decrementedCount = new BsonDocument("$subtract", new BsonArray { currentCount, amount });
        var stage = new BsonDocument("$set", new BsonDocument
        {
            { "Count", new BsonDocument("$max", new BsonArray { 0L, decrementedCount }) }
        });

        var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(new[] { stage });
        return Builders<BsonDocument>.Update.Pipeline(pipeline);
    }

    private async Task BulkWriteCountersAsync(
        IReadOnlyCollection<WriteModel<BsonDocument>> models,
        CancellationToken cancellationToken)
    {
        if (models.Count > 0)
            await CounterCollection.BulkWriteAsync(models, cancellationToken: cancellationToken);
    }

    private static BsonDocument CreateCounterDocument(string key, long count, DateTime windowStart)
    {
        return new BsonDocument
        {
            { "_id", key },
            { "Count", count },
            { "WindowStart", windowStart }
        };
    }

    private static long GetCounterCount(BsonDocument document) => document["Count"].AsInt64;

    private static DateTime GetCounterWindowStart(BsonDocument document) => document["WindowStart"].ToUniversalTime();
}
