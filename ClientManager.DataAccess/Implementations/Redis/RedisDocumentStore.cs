using System.Text.Json;
using ClientManager.DataAccess.Interfaces;
using StackExchange.Redis;

namespace ClientManager.DataAccess.Implementations.Redis;

/// <summary>
/// Redis-based implementation of <see cref="IDocumentStore"/>.
/// Each collection maps to a Redis hash. Counters use native Redis INCR with key expiry.
/// </summary>
public class RedisDocumentStore : IDocumentStore
{
    private readonly IConnectionMultiplexer _redis;
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
    }

    private IDatabase Database => _redis.GetDatabase();

    private static string HashKey(string collection) => $"collection:{collection}";
    private const string CounterPrefix = "counter:";

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class
    {
        var value = await Database.HashGetAsync(HashKey(collection), id);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value!, JsonOptions);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class
    {
        var entries = await Database.HashGetAllAsync(HashKey(collection));
        return entries
            .Select(e => JsonSerializer.Deserialize<T>(e.Value!, JsonOptions)!)
            .ToList();
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class
    {
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await Database.HashSetAsync(HashKey(collection), id, json);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        await Database.HashDeleteAsync(HashKey(collection), id);
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
    public async Task ResetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var redisKey = $"{CounterPrefix}{key}";
        await Database.KeyDeleteAsync(redisKey);
    }
}
