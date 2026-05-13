using System.Collections.Concurrent;
using System.Text.Json;
using ClientManager.DataAccess.Stores.Implementations.Helpers;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
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
    private readonly bool _hasRediSearch;
    private readonly ConcurrentDictionary<string, bool> _createdIndexes = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="RedisDocumentStore"/>.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    public RedisDocumentStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _hasRediSearch = DetectRediSearch();
    }

    private IDatabase Database => _redis.GetDatabase();

    private static string HashKey(string collection) => $"collection:{collection}";
    private static string DocKey(string collection, string id) => $"doc:{collection}:{id}";
    private static string IndexName(string collection) => $"idx:{collection}";
    private const string CounterPrefix = "counter:";
    private const string JsonRootPath = "$";

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class
    {
        if (_hasRediSearch)
        {
            var json = await Database.JSON().GetAsync(DocKey(collection, id), JsonRootPath);
            if (json is null || json.IsNull)
                return null;

            return DeserializeRedisJson<T>(json.ToString());
        }

        var value = await Database.HashGetAsync(HashKey(collection), id);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value!, JsonOptions);
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

        cancellationToken.ThrowIfCancellationRequested();

        return _hasRediSearch
            ? await GetManyFromJsonAsync<T>(collection, requestedIds, cancellationToken)
            : await GetManyFromHashAsync<T>(collection, requestedIds, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class
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
        return [.. entries.Select(e => JsonSerializer.Deserialize<T>(e.Value!, JsonOptions)!)];
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class
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
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        if (_hasRediSearch)
        {
            await Database.KeyDeleteAsync(DocKey(collection, id));
        }
        else
        {
            await Database.HashDeleteAsync(HashKey(collection), id);
        }
    }

    /// <inheritdoc />
    public async Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var redisKey = $"{CounterPrefix}{key}";
        var db = Database;

        var count = await db.StringIncrementAsync(redisKey);

        if (count == 1)
        {
            await db.KeyExpireAsync(redisKey, window);
        }

        return count;
    }

    /// <inheritdoc />
    public async Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var redisKey = $"{CounterPrefix}{key}";
        var value = await Database.StringGetAsync(redisKey);
        return value.IsNullOrEmpty ? 0 : (long)value;
    }

    /// <inheritdoc />
    public async Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var redisKey = $"{CounterPrefix}{key}";
        var count = await Database.StringDecrementAsync(redisKey);
        if (count < 0)
        {
            await Database.StringSetAsync(redisKey, 0);
            return 0;
        }
        return count;
    }

    /// <inheritdoc />
    public async Task ResetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var redisKey = $"{CounterPrefix}{key}";
        await Database.KeyDeleteAsync(redisKey);
    }

    /// <inheritdoc />
    public async Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var redisKey = $"{CounterPrefix}{key}";
        var db = Database;
        await db.StringSetAsync(redisKey, value, window);
    }

    /// <inheritdoc />
    public async Task<SearchResult<T>> SearchAsync<T>(
        string collection, DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
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
    }

    /// <inheritdoc />
    public async Task<long> CountAsync<T>(
        string collection, DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
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

            var item = JsonSerializer.Deserialize<T>(value!, JsonOptions);
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
}
