namespace ClientManager.DataAccess.Stores.Implementations.Helpers;

/// <summary>
/// Shared fallback implementations for storage-backed leader leases.
/// </summary>
internal static class DocumentStoreLeaseDefaults
{
    public const string LeaseCollection = "_leases";

    public static async Task<bool> TryAcquireLeaseAsync(
        Func<string, string, CancellationToken, Task<LeaseRecord?>> getLeaseAsync,
        Func<string, string, LeaseRecord, CancellationToken, Task> setLeaseAsync,
        string key,
        string ownerId,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var existing = await getLeaseAsync(LeaseCollection, key, cancellationToken);
        if (existing is not null &&
            existing.ExpiresAtUtc > now &&
            !string.Equals(existing.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return false;
        }

        await setLeaseAsync(LeaseCollection, key, new LeaseRecord
        {
            OwnerId = ownerId,
            ExpiresAtUtc = now.Add(duration)
        }, cancellationToken);

        return true;
    }

    public static async Task<bool> RenewLeaseAsync(
        Func<string, string, CancellationToken, Task<LeaseRecord?>> getLeaseAsync,
        Func<string, string, LeaseRecord, CancellationToken, Task> setLeaseAsync,
        string key,
        string ownerId,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var existing = await getLeaseAsync(LeaseCollection, key, cancellationToken);
        if (existing is null ||
            existing.ExpiresAtUtc <= now ||
            !string.Equals(existing.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return false;
        }

        await setLeaseAsync(LeaseCollection, key, new LeaseRecord
        {
            OwnerId = ownerId,
            ExpiresAtUtc = now.Add(duration)
        }, cancellationToken);

        return true;
    }

    public static async Task ReleaseLeaseAsync(
        Func<string, string, CancellationToken, Task<LeaseRecord?>> getLeaseAsync,
        Func<string, string, CancellationToken, Task> deleteLeaseAsync,
        string key,
        string ownerId,
        CancellationToken cancellationToken)
    {
        var existing = await getLeaseAsync(LeaseCollection, key, cancellationToken);
        if (existing is null || !string.Equals(existing.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return;
        }

        await deleteLeaseAsync(LeaseCollection, key, cancellationToken);
    }
}

internal sealed class LeaseRecord
{
    public string OwnerId { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}
