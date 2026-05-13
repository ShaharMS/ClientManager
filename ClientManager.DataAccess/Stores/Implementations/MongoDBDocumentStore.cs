using System.Text.Json;
using System.Text.RegularExpressions;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using SearchDirection = ClientManager.Shared.Models.Search.SortDirection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace ClientManager.DataAccess.Stores.Implementations;

/// <summary>
/// MongoDB-based implementation of <see cref="IDocumentStore"/>.
/// Each collection name maps to a MongoDB collection. Counters use a dedicated collection.
/// </summary>
public class MongoDBDocumentStore : IDocumentStore
{
    private readonly IMongoDatabase _database;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="MongoDBDocumentStore"/>.
    /// </summary>
    /// <param name="database">The MongoDB database instance to use.</param>
    public MongoDBDocumentStore(IMongoDatabase database)
    {
        _database = database;
    }

    private IMongoCollection<BsonDocument> GetCollection(string collection) =>
        _database.GetCollection<BsonDocument>(collection);

    private IMongoCollection<BsonDocument> CounterCollection =>
        _database.GetCollection<BsonDocument>("_counters");

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
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var bsonDoc = BsonDocument.Parse(json);
        bsonDoc["_id"] = id;

        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        await GetCollection(collection).ReplaceOneAsync(filter, bsonDoc, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        await GetCollection(collection).DeleteOneAsync(filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);

        var existing = await CounterCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            var windowStart = existing["WindowStart"].ToUniversalTime();
            if (now - windowStart >= window)
            {
                var resetDoc = new BsonDocument
                {
                    { "_id", key },
                    { "Count", 1L },
                    { "WindowStart", now }
                };
                await CounterCollection.ReplaceOneAsync(filter, resetDoc, new ReplaceOptions { IsUpsert = true }, cancellationToken);
                return 1;
            }
        }

        var update = Builders<BsonDocument>.Update
            .Inc("Count", 1L)
            .SetOnInsert("WindowStart", now);

        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var result = await CounterCollection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        return result["Count"].AsInt64;
    }

    /// <inheritdoc />
    public async Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
        var doc = await CounterCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return doc?["Count"].AsInt64 ?? 0;
    }

    /// <inheritdoc />
    public async Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
        var update = Builders<BsonDocument>.Update.Inc("Count", -1L);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After
        };

        var result = await CounterCollection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        if (result is null)
            return 0;

        var count = result["Count"].AsInt64;
        if (count < 0)
        {
            await SetCounterAsync(key, 0, TimeSpan.FromHours(24), cancellationToken);
            return 0;
        }

        return count;
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
}
