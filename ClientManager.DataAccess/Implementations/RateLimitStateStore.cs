using ClientManager.DataAccess.Interfaces;

namespace ClientManager.DataAccess.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IRateLimitStateStore"/>.
/// Delegates all counter operations to <see cref="IDocumentStore"/>.
/// </summary>
public class RateLimitStateStore : IRateLimitStateStore
{
    private readonly IDocumentStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="RateLimitStateStore"/>.
    /// </summary>
    /// <param name="store">The document store to delegate counter operations to.</param>
    public RateLimitStateStore(IDocumentStore store)
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
}
