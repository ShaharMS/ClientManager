using System.Diagnostics;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Search;

namespace ClientManager.StorageApi.Utils.Instrumentation;

/// <summary>
/// Adds StorageApi tracing, metrics, and timing logs around a configured document store.
/// </summary>
public sealed class InstrumentedDocumentStore : IDocumentStore
{
    private const double SlowOperationThresholdMs = 250;
    private const string CounterCollection = "_counters";

    private readonly IDocumentStore _inner;
    private readonly StorageRole _role;
    private readonly PersistenceProvider _provider;
    private readonly StorageApiMetrics _metrics;
    private readonly IAppLogger<InstrumentedDocumentStore> _logger;

    public InstrumentedDocumentStore(
        IDocumentStore inner,
        StorageRole role,
        PersistenceProvider provider,
        StorageApiMetrics metrics,
        IAppLogger<InstrumentedDocumentStore> logger)
    {
        _inner = inner;
        _role = role;
        _provider = provider;
        _metrics = metrics;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "get", () => _inner.GetAsync<T>(collection, id, cancellationToken));

    public Task<IReadOnlyList<T>> GetManyAsync<T>(
        string collection,
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "get_many", () => _inner.GetManyAsync<T>(collection, ids, cancellationToken));

    public Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "get_all", () => _inner.GetAllAsync<T>(collection, cancellationToken));

    public Task SetAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "set", () => _inner.SetAsync(collection, id, document, cancellationToken));

    public Task DeleteAsync(string collection, string id, CancellationToken cancellationToken = default) =>
        TraceAsync(collection, "delete", () => _inner.DeleteAsync(collection, id, cancellationToken));

    public Task<long> IncrementCounterAsync(string key, TimeSpan window, CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_increment", () => _inner.IncrementCounterAsync(key, window, cancellationToken));

    public Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_decrement", () => _inner.DecrementCounterAsync(key, cancellationToken));

    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_get", () => _inner.GetCounterAsync(key, cancellationToken));

    public Task SetCounterAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_set", () => _inner.SetCounterAsync(key, value, window, cancellationToken));

    public Task ResetCounterAsync(string key, CancellationToken cancellationToken = default) =>
        TraceAsync(CounterCollection, "counter_reset", () => _inner.ResetCounterAsync(key, cancellationToken));

    public Task<SearchResult<T>> SearchAsync<T>(
        string collection,
        DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "search", () => _inner.SearchAsync<T>(collection, query, cancellationToken));

    public Task<long> CountAsync<T>(
        string collection,
        DocumentQuery query,
        CancellationToken cancellationToken = default) where T : class =>
        TraceAsync(collection, "count", () => _inner.CountAsync<T>(collection, query, cancellationToken));

    private async Task TraceAsync(string collection, string operation, Func<Task> action)
    {
        await TraceAsync(collection, operation, async () =>
        {
            await action();
            return true;
        });
    }

    private async Task<TResult> TraceAsync<TResult>(
        string collection,
        string operation,
        Func<Task<TResult>> action)
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
            DurationMs = durationMs,
            Result = result,
            LockWaitMs = activity?.GetTagItem("storage.lock_wait_ms")
        };

        if (exception is not null)
        {
            _logger.Error("Document-store operation failed", exception, extraData);
            return;
        }

        if (durationMs >= SlowOperationThresholdMs)
        {
            _logger.Warn("Document-store operation completed slowly", extraData);
            return;
        }

        _logger.Debug("Document-store operation completed", extraData);
    }
}