using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using Microsoft.Data.Sqlite;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Normalized SQLite storage for usage snapshots and pending counters.
/// </summary>
public sealed class SqliteUsageSnapshotDatabase : IUsageSnapshotDatabase, IDisposable
{
    private readonly string _connectionString;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SqliteUsageSnapshotDatabase(
        string databasePath,
        IClientConfigurationDatabase clientConfigDatabase)
    {
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ConnectionString;
        _clientConfigDatabase = clientConfigDatabase;
        EnsureSchema();
    }

    public void Dispose() => _writeLock.Dispose();

    /// <inheritdoc />
    public async Task<UsageSnapshot?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var snapshots = await LoadSnapshotsByIdsAsync([id], cancellationToken);
        return snapshots.FirstOrDefault();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UsageSnapshot>> GetByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) =>
        LoadSnapshotsByIdsAsync(ids.Distinct(StringComparer.Ordinal).ToList(), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSnapshot>> GetByTargetAsync(
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id FROM usage_snapshots
            WHERE target_id = $targetId AND target_type = $targetType AND granularity = $granularity
            """;
        command.Parameters.AddWithValue("$targetId", targetId);
        command.Parameters.AddWithValue("$targetType", targetType.ToString());
        command.Parameters.AddWithValue("$granularity", granularity.ToString());

        var ids = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetString(0));
            }
        }

        return await LoadSnapshotsByIdsAsync(ids, cancellationToken);
    }

    /// <inheritdoc />
    public Task<UsageSnapshot?> GetByClientAndTargetAsync(
        string clientId,
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var id = UsageSnapshotDatabase.BuildId(clientId, targetType, targetId, granularity);
        return GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default) =>
        await UpsertManyAsync([snapshot], cancellationToken);

    /// <inheritdoc />
    public async Task UpsertManyAsync(
        IReadOnlyCollection<UsageSnapshot> snapshots,
        CancellationToken cancellationToken = default)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            foreach (var snapshot in snapshots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UpsertSnapshotAsync(connection, transaction, snapshot, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM usage_snapshots WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSnapshot>> GetAllByGranularityAsync(
        BucketGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM usage_snapshots WHERE granularity = $granularity";
        command.Parameters.AddWithValue("$granularity", granularity.ToString());

        var ids = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetString(0));
            }
        }

        return await LoadSnapshotsByIdsAsync(ids, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSnapshot>> GetByTargetAndRangeAsync(
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
        var clientIds = clients.Select(client => client.Id);
        return await GetByTargetsAndRangeAsync(
            [targetId],
            targetType,
            granularity,
            from,
            to,
            clientIds,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UsageSnapshot>> GetByTargetAndRangeAsync(
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        DateTime from,
        DateTime to,
        IEnumerable<string> clientIds,
        CancellationToken cancellationToken = default) =>
        GetByTargetsAndRangeAsync([targetId], targetType, granularity, from, to, clientIds, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSnapshot>> GetByTargetsAndRangeAsync(
        IEnumerable<string> targetIds,
        TargetType targetType,
        BucketGranularity granularity,
        DateTime from,
        DateTime to,
        IEnumerable<string> clientIds,
        CancellationToken cancellationToken = default)
    {
        var selectedTargetIds = targetIds.Distinct(StringComparer.Ordinal).ToList();
        var selectedClientIds = clientIds.Distinct(StringComparer.Ordinal).ToList();
        if (selectedTargetIds.Count == 0 || selectedClientIds.Count == 0)
        {
            return [];
        }

        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        var targetParams = string.Join(", ", selectedTargetIds.Select((_, index) => $"$t{index}"));
        var clientParams = string.Join(", ", selectedClientIds.Select((_, index) => $"$c{index}"));
        command.CommandText = $"""
            SELECT DISTINCT s.id
            FROM usage_snapshots s
            INNER JOIN usage_buckets b ON b.snapshot_id = s.id
            WHERE s.target_type = $targetType
              AND s.granularity = $granularity
              AND s.target_id IN ({targetParams})
              AND s.client_id IN ({clientParams})
              AND b.bucket_start >= $from
              AND b.bucket_start <= $to
            """;
        command.Parameters.AddWithValue("$targetType", targetType.ToString());
        command.Parameters.AddWithValue("$granularity", granularity.ToString());
        command.Parameters.AddWithValue("$from", from.ToString("O"));
        command.Parameters.AddWithValue("$to", to.ToString("O"));
        for (var index = 0; index < selectedTargetIds.Count; index++)
        {
            command.Parameters.AddWithValue($"$t{index}", selectedTargetIds[index]);
        }

        for (var index = 0; index < selectedClientIds.Count; index++)
        {
            command.Parameters.AddWithValue($"$c{index}", selectedClientIds[index]);
        }

        var ids = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetString(0));
            }
        }

        return await LoadSnapshotsByIdsAsync(ids, cancellationToken);
    }

    /// <inheritdoc />
    public Task<UsageSnapshot?> GetByClientTargetAndSegmentAsync(
        string clientId,
        string targetId,
        TargetType targetType,
        BucketGranularity granularity,
        DateTime segmentStart,
        CancellationToken cancellationToken = default)
    {
        var id = UsageSegmentHelper.BuildSegmentId(clientId, targetType, targetId, granularity, segmentStart);
        return GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task IncrementPendingCountersAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            foreach (var (key, (amount, window)) in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await IncrementCounterAsync(connection, transaction, key, amount, window, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> GetPendingCounterValuesAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var requested = keys.Distinct(StringComparer.Ordinal).ToList();
        var result = new Dictionary<string, long>(requested.Count, StringComparer.Ordinal);
        if (requested.Count == 0)
        {
            return result;
        }

        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        var paramNames = string.Join(", ", requested.Select((_, index) => $"$k{index}"));
        command.CommandText = $"SELECT key, count FROM usage_counters WHERE key IN ({paramNames})";
        for (var index = 0; index < requested.Count; index++)
        {
            command.Parameters.AddWithValue($"$k{index}", requested[index]);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = reader.GetInt64(1);
        }

        foreach (var key in requested)
        {
            result.TryAdd(key, 0);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> GetPendingCounterValuesByPrefixAsync(
        string keyPrefix,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, count FROM usage_counters WHERE key LIKE $prefix AND count > 0";
        command.Parameters.AddWithValue("$prefix", $"{keyPrefix}%");

        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = reader.GetInt64(1);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> GetPendingCountersInRangeAsync(
        string clientId,
        TargetType targetType,
        string targetId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var prefix = UsageSegmentHelper.BuildUsageCounterScanPrefix(clientId, targetType, targetId);
        var counters = await GetPendingCounterValuesByPrefixAsync(prefix, cancellationToken);
        return FilterCountersInSecondRange(counters, from, to);
    }

    /// <inheritdoc />
    public async Task ResetPendingCountersAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var requested = keys.Distinct(StringComparer.Ordinal).ToList();
        if (requested.Count == 0)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await using var command = connection.CreateCommand();
            var paramNames = string.Join(", ", requested.Select((_, index) => $"$k{index}"));
            command.CommandText = $"DELETE FROM usage_counters WHERE key IN ({paramNames})";
            for (var index = 0; index < requested.Count; index++)
            {
                command.Parameters.AddWithValue($"$k{index}", requested[index]);
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS usage_snapshots (
                id TEXT PRIMARY KEY,
                client_id TEXT NOT NULL,
                target_id TEXT NOT NULL,
                target_type TEXT NOT NULL,
                granularity TEXT NOT NULL,
                segment_start TEXT
            );
            CREATE TABLE IF NOT EXISTS usage_buckets (
                snapshot_id TEXT NOT NULL,
                bucket_start TEXT NOT NULL,
                granted INTEGER NOT NULL,
                denied INTEGER NOT NULL,
                denied_unauth INTEGER NOT NULL,
                denied_blocked INTEGER NOT NULL,
                denied_rate_limited INTEGER NOT NULL,
                denied_capacity INTEGER NOT NULL,
                released INTEGER NOT NULL,
                active INTEGER NOT NULL,
                PRIMARY KEY (snapshot_id, bucket_start),
                FOREIGN KEY (snapshot_id) REFERENCES usage_snapshots(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_buckets_query
                ON usage_buckets(snapshot_id, bucket_start);
            CREATE INDEX IF NOT EXISTS ix_snapshots_target
                ON usage_snapshots(target_id, target_type, granularity, client_id);
            CREATE TABLE IF NOT EXISTS usage_counters (
                key TEXT PRIMARY KEY,
                count INTEGER NOT NULL,
                window_start TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private async Task<IReadOnlyList<UsageSnapshot>> LoadSnapshotsByIdsAsync(
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var connection = OpenConnection();
        var snapshots = new Dictionary<string, UsageSnapshot>(ids.Count, StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            var paramNames = string.Join(", ", ids.Select((_, index) => $"$id{index}"));
            command.CommandText = $"""
                SELECT id, client_id, target_id, target_type, granularity, segment_start
                FROM usage_snapshots WHERE id IN ({paramNames})
                """;
            for (var index = 0; index < ids.Count; index++)
            {
                command.Parameters.AddWithValue($"$id{index}", ids[index]);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                snapshots[id] = new UsageSnapshot
                {
                    Id = id,
                    ClientId = reader.GetString(1),
                    TargetId = reader.GetString(2),
                    TargetType = Enum.Parse<TargetType>(reader.GetString(3)),
                    Granularity = Enum.Parse<BucketGranularity>(reader.GetString(4)),
                    SegmentStart = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)).ToUniversalTime(),
                    Buckets = []
                };
            }
        }

        await using (var command = connection.CreateCommand())
        {
            var paramNames = string.Join(", ", ids.Select((_, index) => $"$id{index}"));
            command.CommandText = $"""
                SELECT snapshot_id, bucket_start, granted, denied, denied_unauth, denied_blocked,
                       denied_rate_limited, denied_capacity, released, active
                FROM usage_buckets WHERE snapshot_id IN ({paramNames})
                ORDER BY bucket_start
                """;
            for (var index = 0; index < ids.Count; index++)
            {
                command.Parameters.AddWithValue($"$id{index}", ids[index]);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var snapshotId = reader.GetString(0);
                if (!snapshots.TryGetValue(snapshotId, out var snapshot))
                {
                    continue;
                }

                snapshot.Buckets.Add(new UsageBucket
                {
                    Timestamp = DateTime.Parse(reader.GetString(1)).ToUniversalTime(),
                    GrantedCount = reader.GetInt64(2),
                    DeniedCount = reader.GetInt64(3),
                    DeniedUnauthenticatedCount = reader.GetInt64(4),
                    DeniedBlockedCount = reader.GetInt64(5),
                    DeniedRateLimitedCount = reader.GetInt64(6),
                    DeniedCapacityLimitedCount = reader.GetInt64(7),
                    ReleasedCount = reader.GetInt64(8),
                    ActiveCount = reader.GetInt64(9)
                });
            }
        }

        return snapshots.Values.ToList();
    }

    private static async Task UpsertSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        UsageSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO usage_snapshots (id, client_id, target_id, target_type, granularity, segment_start)
                VALUES ($id, $clientId, $targetId, $targetType, $granularity, $segmentStart)
                ON CONFLICT(id) DO UPDATE SET
                    client_id = excluded.client_id,
                    target_id = excluded.target_id,
                    target_type = excluded.target_type,
                    granularity = excluded.granularity,
                    segment_start = excluded.segment_start
                """;
            command.Parameters.AddWithValue("$id", snapshot.Id);
            command.Parameters.AddWithValue("$clientId", snapshot.ClientId);
            command.Parameters.AddWithValue("$targetId", snapshot.TargetId);
            command.Parameters.AddWithValue("$targetType", snapshot.TargetType.ToString());
            command.Parameters.AddWithValue("$granularity", snapshot.Granularity.ToString());
            command.Parameters.AddWithValue(
                "$segmentStart",
                snapshot.SegmentStart is null ? DBNull.Value : snapshot.SegmentStart.Value.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var bucket in snapshot.Buckets)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO usage_buckets (
                    snapshot_id, bucket_start, granted, denied, denied_unauth, denied_blocked,
                    denied_rate_limited, denied_capacity, released, active)
                VALUES (
                    $snapshotId, $bucketStart, $granted, $denied, $deniedUnauth, $deniedBlocked,
                    $deniedRateLimited, $deniedCapacity, $released, $active)
                ON CONFLICT(snapshot_id, bucket_start) DO UPDATE SET
                    granted = excluded.granted,
                    denied = excluded.denied,
                    denied_unauth = excluded.denied_unauth,
                    denied_blocked = excluded.denied_blocked,
                    denied_rate_limited = excluded.denied_rate_limited,
                    denied_capacity = excluded.denied_capacity,
                    released = excluded.released,
                    active = excluded.active
                """;
            command.Parameters.AddWithValue("$snapshotId", snapshot.Id);
            command.Parameters.AddWithValue("$bucketStart", bucket.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("$granted", bucket.GrantedCount);
            command.Parameters.AddWithValue("$denied", bucket.DeniedCount);
            command.Parameters.AddWithValue("$deniedUnauth", bucket.DeniedUnauthenticatedCount);
            command.Parameters.AddWithValue("$deniedBlocked", bucket.DeniedBlockedCount);
            command.Parameters.AddWithValue("$deniedRateLimited", bucket.DeniedRateLimitedCount);
            command.Parameters.AddWithValue("$deniedCapacity", bucket.DeniedCapacityLimitedCount);
            command.Parameters.AddWithValue("$released", bucket.ReleasedCount);
            command.Parameters.AddWithValue("$active", bucket.ActiveCount);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task IncrementCounterAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        long amount,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO usage_counters (key, count, window_start)
            VALUES ($key, $amount, $windowStart)
            ON CONFLICT(key) DO UPDATE SET
                count = CASE
                    WHEN (julianday('now') - julianday(window_start)) * 86400.0 < $windowSeconds
                    THEN count + $amount
                    ELSE $amount
                END,
                window_start = CASE
                    WHEN (julianday('now') - julianday(window_start)) * 86400.0 < $windowSeconds
                    THEN window_start
                    ELSE $now
                END
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$amount", amount);
        command.Parameters.AddWithValue("$windowStart", now);
        command.Parameters.AddWithValue("$windowSeconds", window.TotalSeconds);
        command.Parameters.AddWithValue("$now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, long> FilterCountersInSecondRange(
        IReadOnlyDictionary<string, long> counters,
        DateTime from,
        DateTime to)
    {
        var start = UsageSegmentHelper.RoundDownToSecond(from);
        var end = UsageSegmentHelper.RoundDownToSecond(to);
        var result = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var (key, value) in counters)
        {
            if (value <= 0 ||
                !UsageSegmentHelper.TryParseUsageCounterKey(
                    key, out _, out _, out _, out var secondTimestamp, out _, out _))
            {
                continue;
            }

            if (secondTimestamp < start || secondTimestamp > end)
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<long> CountAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM usage_snapshots";
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSnapshot>> GetPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id FROM usage_snapshots
            ORDER BY id
            LIMIT $take OFFSET $skip
            """;
        command.Parameters.AddWithValue("$skip", skip);
        command.Parameters.AddWithValue("$take", take);

        var ids = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetString(0));
            }
        }

        return await LoadSnapshotsByIdsAsync(ids, cancellationToken);
    }
}
