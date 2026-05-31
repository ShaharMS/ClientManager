namespace ClientManager.Api.Utils.StorageApi;

/// <summary>
/// Declares per-request transport contract flags for calls to the internal storage-facing API.
/// The resilience handler reads these flags instead of inferring behavior from the request URI,
/// so retry decisions are an explicit property of each call rather than a guess based on the path.
/// </summary>
public static class StorageApiRequestOptions
{
    /// <summary>
    /// Marks a request as safe to retry on transient transport failures.
    /// Read operations expressed as non-idempotent verbs (for example, search expressed as POST)
    /// set this flag to opt in; mutating operations leave it unset so they are never retried.
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> Retryable = new("StorageApi.Retryable");
}
