using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.DataAccess.Databases.Interfaces;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IRateLimitStateDatabase"/>.
/// Delegates all counter operations to <see cref="IDocumentStore"/>.
/// </summary>
public class RateLimitStateDatabase : IRateLimitStateDatabase
{
    private readonly IDocumentStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="RateLimitStateDatabase"/>.
    /// </summary>
    /// <param name="store">The document store to delegate counter operations to.</param>
    public RateLimitStateDatabase(IDocumentStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken = default) =>
        _store.IncrementCounterAsync(key, window, cancellationToken);

    /// <inheritdoc />
    public Task<long> GetCountAsync(string key, CancellationToken cancellationToken = default) =>
        _store.GetCounterAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task SetCountAsync(string key, long value, TimeSpan window, CancellationToken cancellationToken = default) =>
        _store.SetCounterAsync(key, value, window, cancellationToken);

    /// <inheritdoc />
    public Task ResetAsync(string key, CancellationToken cancellationToken = default) =>
        _store.ResetCounterAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, long>> GetMultipleCountsAsync(
        IEnumerable<string> keys, CancellationToken cancellationToken = default) =>
        _store.GetManyCountersAsync(keys, cancellationToken);

    /// <inheritdoc />
    public Task SetMultipleCountsAsync(
        IReadOnlyDictionary<string, (long value, TimeSpan window)> entries,
        CancellationToken cancellationToken = default) =>
        _store.SetManyCountersAsync(entries, cancellationToken);
}
