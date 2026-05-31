using System.Net.Http.Json;

namespace ClientManager.Api.Utils.StorageApi;

/// <summary>
/// Helpers for issuing storage-facing API requests that declare their transport contract explicitly.
/// </summary>
public static class StorageApiRequestExtensions
{
    /// <summary>
    /// Sends a JSON POST that is declared safe to retry on transient transport failures.
    /// Used by read operations that are modeled as POST (such as search) so the resilience handler
    /// can retry them without inferring retryability from the request path.
    /// </summary>
    /// <typeparam name="TValue">The type of the request payload to serialize as JSON.</typeparam>
    /// <param name="httpClient">The typed storage-facing HTTP client.</param>
    /// <param name="requestUri">The relative storage API route to post to.</param>
    /// <param name="value">The request payload serialized into the JSON body.</param>
    /// <param name="cancellationToken">Token used to cancel the outbound request.</param>
    /// <returns>The HTTP response from the storage-facing API.</returns>
    public static Task<HttpResponseMessage> PostRetryableAsJsonAsync<TValue>(
        this HttpClient httpClient,
        string requestUri,
        TValue value,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(value)
        };
        request.Options.Set(StorageApiRequestOptions.Retryable, true);
        return httpClient.SendAsync(request, cancellationToken);
    }
}
