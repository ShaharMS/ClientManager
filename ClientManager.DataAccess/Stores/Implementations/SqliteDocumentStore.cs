using System.Collections.Concurrent;
using System.Text.Json;
using ClientManager.DataAccess.Stores.Implementations.Helpers;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using Microsoft.Data.Sqlite;
using static ClientManager.DataAccess.Stores.Implementations.Helpers.StoreSerialization;

namespace ClientManager.DataAccess.Stores.Implementations;

/// <summary>
/// SQLite implementation of <see cref="IDocumentStore"/>.
/// Documents and counters live in one database file; queries filter in memory after fetch.
/// </summary>
public sealed class SqliteDocumentStore : IDocumentStore
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SqliteDocumentStore(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ConnectionString;
        EnsureSchema();
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM documents WHERE collection = $collection AND id = $id";
        command.Parameters.AddWithValue("$collection", collection);
        command.Parameters.AddWithValue("$id", id);

        var json = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<T>(json, JsonOptions);
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

        var results = new List<T>(requestedIds.Count);
        foreach (var batch in requestedIds.Chunk(250))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var placeholders = string.Join(',', batch.Select((_, index) => $"$id{index}"));
            await using var connection = OpenConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT json FROM documents WHERE collection = $collection AND id IN ({placeholders})";
            command.Parameters.AddWithValue("$collection", collection);
            for (var index = 0; index < batch.Length; index++)
            {
                command.Parameters.AddWithValue($"$id{index}", batch[index]);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var item = JsonSerializer.Deserialize<T>(reader.GetString(0), JsonOptions);
                if (item is not null)
                {
                    results.Add(item);
                }
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM documents WHERE collection = $collection";
        command.Parameters.AddWithValue("$collection", collection);

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = JsonSerializer.Deserialize<T>(reader.GetString(0), JsonOptions);
            if (item is not null)
            {
                results.Add(item);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class
    {
        await SetManyAsync(collection, new Dictionary<string, T>(StringComparer.Ordinal) { [id] = document }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetManyAsync<T>(
        string collection,
        IReadOnlyDictionary<string, T> documents,
        CancellationToken cancellationToken = default) where T : class
    {
        if (documents.Count == 0)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO documents (collection, id, json)
                VALUES ($collection, $id, $json)
                ON CONFLICT(collection, id) DO UPDATE SET json = excluded.json
                """;
            var collectionParameter = command.CreateParameter();
            collectionParameter.ParameterName = "$collection";
            command.Parameters.Add(collectionParameter);
            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "$id";
            command.Parameters.Add(idParameter);
            var jsonParameter = command.CreateParameter();
            jsonParameter.ParameterName = "$json";
            command.Parameters.Add(jsonParameter);

            collectionParameter.Value = collection;
            foreach (var (id, document) in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                idParameter.Value = id;
                jsonParameter.Value = JsonSerializer.Serialize(document, JsonOptions);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
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
            await using var connection = OpenConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM documents WHERE collection = $collection AND id = $id";
            command.Parameters.AddWithValue("$collection", collection);
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
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
    public Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default) =>
        IncrementManyCountersAsync(new Dictionary<string, (long amount, TimeSpan window)> { [key] = (1, window) }, cancellationToken)
            .ContinueWith(task => task.Result[key], cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> IncrementManyCountersAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var result = new Dictionary<string, long>(entries.Count, StringComparer.Ordinal);
            await using var connection = OpenConnection();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            foreach (var (key, (amount, window)) in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result[key] = await UpsertCounterAsync(connection, transaction, key, amount, window, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await DecrementManyCountersAsync(new Dictionary<string, long> { [key] = 1 }, cancellationToken);
        return result[key];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> DecrementManyCountersAsync(
        IReadOnlyDictionary<string, long> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var result = new Dictionary<string, long>(entries.Count, StringComparer.Ordinal);
            await using var connection = OpenConnection();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            foreach (var (key, amount) in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = await ReadCounterValueAsync(connection, transaction, key, cancellationToken);
                var next = Math.Max(0, current - Math.Max(0, amount));
                await WriteCounterAsync(connection, transaction, key, next, DateTime.UtcNow, cancellationToken);
                result[key] = next;
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        var counters = await GetManyCountersAsync([key], cancellationToken);
        return counters.TryGetValue(key, out var value) ? value : 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> GetManyCountersAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var requestedKeys = keys.Distinct(StringComparer.Ordinal).ToList();
        var result = new Dictionary<string, long>(requestedKeys.Count, StringComparer.Ordinal);
        if (requestedKeys.Count == 0)
        {
            return result;
        }

        await using var connection = OpenConnection();
        foreach (var batch in requestedKeys.Chunk(250))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var placeholders = string.Join(',', batch.Select((_, index) => $"$key{index}"));
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT key, count, window_start FROM counters WHERE key IN ({placeholders})";
            for (var index = 0; index < batch.Length; index++)
            {
                command.Parameters.AddWithValue($"$key{index}", batch[index]);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = reader.GetString(0);
                var count = reader.GetInt64(1);
                var windowStart = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind);
                if (DateTime.UtcNow - windowStart < TimeSpan.FromDays(365))
                {
                    result[key] = count;
                }
            }
        }

        foreach (var key in requestedKeys)
        {
            result.TryAdd(key, 0);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> GetCountersByPrefixAsync(
        string keyPrefix,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, count, window_start FROM counters WHERE key LIKE $prefix ESCAPE '\\' AND count > 0";
        command.Parameters.AddWithValue("$prefix", EscapeLikePrefix(keyPrefix) + "%");

        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = reader.GetInt64(1);
        }

        return result;
    }

    /// <inheritdoc />
    public Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default) =>
        SetManyCountersAsync(new Dictionary<string, (long value, TimeSpan window)> { [key] = (value, window) }, cancellationToken);

    /// <inheritdoc />
    public async Task SetManyCountersAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            await using var connection = OpenConnection();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            foreach (var (key, (value, _)) in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteCounterAsync(connection, transaction, key, value, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public Task ResetCounterAsync(string key, CancellationToken cancellationToken = default) =>
        ResetManyCountersAsync([key], cancellationToken);

    /// <inheritdoc />
    public async Task ResetManyCountersAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            foreach (var batch in keys.Distinct(StringComparer.Ordinal).Chunk(250))
            {
                var placeholders = string.Join(',', batch.Select((_, index) => $"$key{index}"));
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"DELETE FROM counters WHERE key IN ({placeholders})";
                for (var index = 0; index < batch.Length; index++)
                {
                    command.Parameters.AddWithValue($"$key{index}", batch[index]);
                }

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> PurgeCountersByPrefixAsync(
        string keyPrefix,
        Func<string, long, DateTime?, bool> shouldPurge,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await using var select = connection.CreateCommand();
            select.CommandText = "SELECT key, count, window_start FROM counters WHERE key LIKE $prefix ESCAPE '\\'";
            select.Parameters.AddWithValue("$prefix", EscapeLikePrefix(keyPrefix) + "%");

            var keysToRemove = new List<string>();
            await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var key = reader.GetString(0);
                    var count = reader.GetInt64(1);
                    var windowStart = DateTime.Parse(
                        reader.GetString(2),
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind);
                    if (shouldPurge(key, count, windowStart))
                    {
                        keysToRemove.Add(key);
                    }
                }
            }

            if (keysToRemove.Count == 0)
            {
                return 0;
            }

            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            foreach (var batch in keysToRemove.Chunk(250))
            {
                var placeholders = string.Join(',', batch.Select((_, index) => $"$key{index}"));
                await using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = $"DELETE FROM counters WHERE key IN ({placeholders})";
                for (var index = 0; index < batch.Length; index++)
                {
                    delete.Parameters.AddWithValue($"$key{index}", batch[index]);
                }

                await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return keysToRemove.Count;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SearchResult<T>> SearchAsync<T>(
        string collection,
        DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
    {
        // ponytail: collection scan + in-memory filter; upgrade path is SQL indexes per field
        var all = await GetAllAsync<T>(collection, cancellationToken);
        return InMemoryQueryEvaluator.Apply(all, query);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync<T>(
        string collection,
        DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class
    {
        if (query.Filters.Count == 0 && query.TextSearch is null && query.Skip is null && query.Take is null && query.Sort is null)
        {
            await using var connection = OpenConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM documents WHERE collection = $collection";
            command.Parameters.AddWithValue("$collection", collection);
            return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        var result = await SearchAsync<T>(collection, query, cancellationToken);
        return result.TotalCount;
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS documents (
                collection TEXT NOT NULL,
                id TEXT NOT NULL,
                json TEXT NOT NULL,
                PRIMARY KEY (collection, id)
            );
            CREATE INDEX IF NOT EXISTS idx_documents_collection ON documents(collection);
            CREATE TABLE IF NOT EXISTS counters (
                key TEXT PRIMARY KEY,
                count INTEGER NOT NULL,
                window_start TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static string EscapeLikePrefix(string prefix) =>
        prefix.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static async Task<long> UpsertCounterAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        long amount,
        TimeSpan window,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var current = await ReadCounterRowAsync(connection, transaction, key, cancellationToken);
        var count = current is not null && now - current.Value.windowStart < window
            ? current.Value.count + amount
            : amount;
        await WriteCounterAsync(connection, transaction, key, count, now, cancellationToken);
        return count;
    }

    private static async Task<long> ReadCounterValueAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string key,
        CancellationToken cancellationToken)
    {
        var row = await ReadCounterRowAsync(connection, transaction, key, cancellationToken);
        return row?.count ?? 0;
    }

    private static async Task<(long count, DateTime windowStart)?> ReadCounterRowAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        command.CommandText = "SELECT count, window_start FROM counters WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (reader.GetInt64(0), DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    private static async Task WriteCounterAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        long count,
        DateTime windowStart,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO counters (key, count, window_start)
            VALUES ($key, $count, $windowStart)
            ON CONFLICT(key) DO UPDATE SET count = excluded.count, window_start = excluded.window_start
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$count", count);
        command.Parameters.AddWithValue("$windowStart", windowStart.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
