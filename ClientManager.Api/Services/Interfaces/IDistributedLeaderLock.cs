namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// A lease held while a pod acts as the leader for a background worker.
/// </summary>
public interface IDistributedLeaderLease : IAsyncDisposable
{
    /// <summary>
    /// Renews the lease so the holder remains leader.
    /// </summary>
    Task RenewAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Acquires exclusive leadership for background workers across API instances.
/// </summary>
public interface IDistributedLeaderLock
{
    /// <summary>
    /// Attempts to acquire leadership for the named worker.
    /// </summary>
    /// <returns>A lease when leadership is acquired; otherwise <c>null</c>.</returns>
    Task<IDistributedLeaderLease?> TryAcquireAsync(string workerName, CancellationToken cancellationToken = default);
}
