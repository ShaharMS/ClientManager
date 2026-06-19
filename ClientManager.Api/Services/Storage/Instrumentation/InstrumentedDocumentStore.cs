using System.Diagnostics;
using System.Text;
using ClientManager.DataAccess.Stores.Implementations;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Storage.Instrumentation;

/// <summary>
/// Adds storage-domain tracing, metrics, and timing logs around a configured document store.
/// </summary>
public sealed class InstrumentedDocumentStore : IDocumentStore
{
    private const double SlowOperationThresholdMs = 250;
    private const string CounterCollection = "_counters";
    private const string LeaseCollection = "_leases";

    private readonly IDocumentStore _inner;
    private readonly StorageRole _role;
    private readonly PersistenceProvider _provider;
    private readonly PersistenceProvider _configuredProvider;
    private readonly StorageMetrics _metrics;
    private readonly IAppLogger<InstrumentedDocumentStore> _logger;
    private readonly string _storeType;

    public InstrumentedDocumentStore(
        IDocumentStore inner,
        StorageRole role,
        PersistenceProvider provider,
        StorageMetrics metrics,
        IAppLogger<InstrumentedDocumentStore> logger)
    {
        _inner = inner;
        _role = role;
        _configuredProvider = provider;
        _provider = ResolveProvider(inner, provider);
        _metrics = metrics;
        _logger = logger;
        _storeType = inner.GetType().Name;
    }

    public Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "get", () => _inner.GetAsync<T>(collection, id, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<T>> GetManyAsync<T>(
        string collection,
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "get_many", () => _inner.GetManyAsync<T>(collection, ids, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "get_all", () => _inner.GetAllAsync<T>(collection, cancellationToken), cancellationToken);

    public Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "set", () => _inner.SetAsync(collection, id, document, cancellationToken), cancellationToken);

    public Task SetManyAsync<T>(
        string collection,
        IReadOnlyDictionary<string, T> documents,
        CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "set_many", () => _inner.SetManyAsync(collection, documents, cancellationToken), cancellationToken);

    public Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default) =>
        TraceAsync(collection, "delete", () => _inner.DeleteAsync(collection, id, cancellationToken), cancellationToken);

    public Task<bool> SetIfFieldEqualsAsync<T>(
        string collection,
        string id,
        T document,
        string fieldName,
        object? expectedValue,
        CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "set_if", () => _inner.SetIfFieldEqualsAsync(collection, id, document, fieldName, expectedValue, cancellationToken), cancellationToken);

    public Task<bool> TryIncrementWithinLimitsAsync(
        IReadOnlyList<(string key, long max, TimeSpan window)> counters,
        CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_try_increment_within_limits", () => _inner.TryIncrementWithinLimitsAsync(counters, cancellationToken), cancellationToken);

    public Task<(bool IsAllowed, long RemainingTokens, long RetryAfterSeconds)> TryConsumeTokenBucketAsync(
        string tokensKey,
        string lastRefillKey,
        int bucketCapacity,
        int tokensPerRefill,
        long refillIntervalSeconds,
        TimeSpan stateWindow,
        long nowUnixSeconds,
        CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_try_consume_token_bucket", () => _inner.TryConsumeTokenBucketAsync(tokensKey, lastRefillKey, bucketCapacity, tokensPerRefill, refillIntervalSeconds, stateWindow, nowUnixSeconds, cancellationToken), cancellationToken);

    public Task<bool> TryAcquireLeaseAsync(
        string key,
        string ownerId,
        TimeSpan duration,
        CancellationToken cancellationToken = default) =>
        TraceAsync(LeaseCollection, "lease_acquire", () => _inner.TryAcquireLeaseAsync(key, ownerId, duration, cancellationToken), cancellationToken);

    public Task<bool> RenewLeaseAsync(
        string key,
        string ownerId,
        TimeSpan duration,
        CancellationToken cancellationToken = default) =>
        TraceAsync(LeaseCollection, "lease_renew", () => _inner.RenewLeaseAsync(key, ownerId, duration, cancellationToken), cancellationToken);

    public Task ReleaseLeaseAsync(string key, string ownerId, CancellationToken cancellationToken = default) =>
        TraceAsync(LeaseCollection, "lease_release", () => _inner.ReleaseLeaseAsync(key, ownerId, cancellationToken), cancellationToken);

    public Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_increment", () => _inner.IncrementCounterAsync(key, window, cancellationToken), cancellationToken);

    public Task<IReadOnlyDictionary<string, long>> IncrementManyCountersAsync(
        IReadOnlyDictionary<string, (long amount, TimeSpan window)> entries,
        CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_increment_many", () => _inner.IncrementManyCountersAsync(entries, cancellationToken), cancellationToken);

    public Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_decrement", () => _inner.DecrementCounterAsync(key, cancellationToken), cancellationToken);

    public Task<IReadOnlyDictionary<string, long>> DecrementManyCountersAsync(
        IReadOnlyDictionary<string, long> entries,
        CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_decrement_many", () => _inner.DecrementManyCountersAsync(entries, cancellationToken), cancellationToken);

    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_get", () => _inner.GetCounterAsync(key, cancellationToken), cancellationToken);

    public Task<IReadOnlyDictionary<string, long>> GetManyCountersAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_get_many", () => _inner.GetManyCountersAsync(keys, cancellationToken), cancellationToken);

    public Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_set", () => _inner.SetCounterAsync(key, value, window, cancellationToken), cancellationToken);

    public Task SetManyCountersAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_set_many", () => _inner.SetManyCountersAsync(entries, cancellationToken), cancellationToken);

    public Task ResetCounterAsync(string key, CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_reset", () => _inner.ResetCounterAsync(key, cancellationToken), cancellationToken);

    public Task<SearchResult<T>> SearchAsync<T>(
        string collection,
        DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "search", () => _inner.SearchAsync<T>(collection, query, cancellationToken), cancellationToken);

    public Task<long> CountAsync<T>(
        string collection,
        DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "count", () => _inner.CountAsync<T>(collection, query, cancellationToken), cancellationToken);

    private async Task TraceAsync(
        string collection,
        string operation,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        await TraceAsync(collection, operation, async () =>
        {
            await action();
            return true;
        }, cancellationToken);
    }

    private async Task<TResult> TraceAsync<TResult>(
        string collection,
        string operation,
        Func<Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        using var activity = StartActivity(collection, operation);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await action();
            stopwatch.Stop();
            Complete(activity, collection, operation, stopwatch.Elapsed.TotalMilliseconds, "success");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            Complete(activity, collection, operation, stopwatch.Elapsed.TotalMilliseconds, "canceled");
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            Complete(activity, collection, operation, stopwatch.Elapsed.TotalMilliseconds, "failure", exception);
            throw;
        }
    }

    private Activity? StartActivity(string collection, string operation)
    {
        var activity = _metrics.ActivitySource.StartActivity(
            $"storage.document_store.{operation}",
            ActivityKind.Internal);

        activity?.SetTag("storage.collection", collection);
        activity?.SetTag("storage.operation", operation);
        activity?.SetTag("storage.role", _role.ToString());
        activity?.SetTag("storage.provider", _provider.ToString());
        return activity;
    }

    private void Complete(
        Activity? activity,
        string collection,
        string operation,
        double durationMs,
        string result,
        Exception? exception = null)
    {
        activity?.SetTag("storage.success", result == "success");
        activity?.SetTag("operation.result", result);
        activity?.SetTag("duration_ms", durationMs);

        if (exception is not null)
        {
            activity?.SetTag("error.type", exception.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error);
        }

        RecordDuration(collection, operation, durationMs, result);
        LogCompletion(activity, collection, operation, durationMs, result, exception);
    }

    private void RecordDuration(string collection, string operation, double durationMs, string result)
    {
        _metrics.DocumentStoreOperationDuration.Record(durationMs, new TagList
        {
            { "collection", collection },
            { "operation", operation },
            { "role", _role.ToString() },
            { "provider", _provider.ToString() },
            { "result", result }
        });
    }

    private void LogCompletion(
        Activity? activity,
        string collection,
        string operation,
        double durationMs,
        string result,
        Exception? exception)
    {
        var extraData = new
        {
            Collection = collection,
            Operation = operation,
            Role = _role.ToString(),
            Provider = _provider.ToString(),
            ConfiguredProvider = _provider == _configuredProvider ? null : _configuredProvider.ToString(),
            StoreType = _storeType,
            DurationMs = durationMs,
            Result = result,
            LockWaitMs = activity?.GetTagItem("storage.lock_wait_ms"),
            ExceptionType = exception?.GetType().Name,
            ExceptionMessage = exception?.Message,
            ExceptionContext = exception is null ? null : DescribeExceptionContext(exception)
        };

        if (result == "canceled")
        {
            _logger.Debug("Document-store operation canceled", extraData);
            return;
        }

        if (exception is not null)
        {
            _logger.Error("Document-store operation failed", extraData, exception);
            return;
        }

        if (durationMs >= SlowOperationThresholdMs)
        {
            _logger.Warn("Document-store operation completed slowly", extraData);
            return;
        }

        _logger.Debug("Document-store operation completed", extraData);
    }

    private static PersistenceProvider ResolveProvider(IDocumentStore inner, PersistenceProvider configuredProvider)
    {
        return inner switch
        {
            RedisDocumentStore => PersistenceProvider.Redis,
            MongoDBDocumentStore => PersistenceProvider.MongoDb,
            JsonFileDocumentStore => PersistenceProvider.JsonFile,
            LuceneDocumentStore => PersistenceProvider.Lucene,
            _ => configuredProvider
        };
    }

    private static string? DescribeExceptionContext(Exception exception)
    {
        if (exception.Data.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();

        foreach (System.Collections.DictionaryEntry entry in exception.Data)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(entry.Key);
            builder.Append('=');
            builder.Append(entry.Value);
        }

        return builder.ToString();
    }
}