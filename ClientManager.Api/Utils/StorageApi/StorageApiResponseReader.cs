using ClientManager.Shared.Models.Problems;

namespace ClientManager.Api.Utils.StorageApi;

/// <summary>
/// Reads and interprets HTTP responses returned by the internal storage-facing API.
/// Centralizes JSON payload deserialization, RFC 7807 problem parsing, retry-after extraction,
/// and the construction of transport-level exceptions so the typed clients share consistent behavior.
/// </summary>
internal static class StorageApiResponseReader
{
    /// <summary>
    /// Deserializes a required JSON payload, treating a missing or null body as an error.
    /// </summary>
    /// <typeparam name="T">The expected payload type.</typeparam>
    /// <param name="response">The storage API response to read.</param>
    /// <param name="cancellationToken">Token used to cancel reading the response body.</param>
    /// <param name="missingPayloadErrorMessage">Message describing the operation whose body was unexpectedly empty.</param>
    /// <returns>The deserialized payload.</returns>
    public static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string missingPayloadErrorMessage)
    {
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException(missingPayloadErrorMessage);
    }

    /// <summary>
    /// Reads an RFC 7807 problem payload, synthesizing a fallback when the body is missing or unparsable.
    /// </summary>
    /// <param name="response">The storage API error response to read.</param>
    /// <param name="cancellationToken">Token used to cancel reading the response body.</param>
    /// <returns>The parsed problem details, or a generated fallback describing the status code.</returns>
    public static async Task<StorageProblemResponse> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<StorageProblemResponse>(cancellationToken)
            ?? new StorageProblemResponse
            {
                Title = "Unexpected Storage API Response",
                Status = (int)response.StatusCode,
                Detail = $"The storage API returned status {(int)response.StatusCode}."
            };
    }

    /// <summary>
    /// Builds a transport exception describing an unexpected storage API failure from an already-read problem.
    /// </summary>
    /// <param name="response">The storage API error response.</param>
    /// <param name="problem">The problem details previously read from the response.</param>
    /// <returns>An <see cref="HttpRequestException"/> carrying the problem detail and status code.</returns>
    public static Exception CreateUnexpectedException(
        HttpResponseMessage response,
        StorageProblemResponse problem)
    {
        var detail = problem.Detail ?? $"The storage API returned status {(int)response.StatusCode}.";
        return new HttpRequestException(detail, null, response.StatusCode);
    }

    /// <summary>
    /// Reads the problem payload and builds a transport exception describing an unexpected storage API failure.
    /// </summary>
    /// <param name="response">The storage API error response.</param>
    /// <param name="cancellationToken">Token used to cancel reading the response body.</param>
    /// <returns>An exception carrying the problem detail and status code.</returns>
    public static async Task<Exception> CreateUnexpectedExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var problem = await ReadProblemAsync(response, cancellationToken);
        return CreateUnexpectedException(response, problem);
    }

    /// <summary>
    /// Extracts a positive retry-after delay in seconds from the response headers, if present.
    /// Supports delta-seconds, HTTP-date, and raw header forms.
    /// </summary>
    /// <param name="response">The storage API response that may carry a Retry-After header.</param>
    /// <returns>The retry-after delay in seconds, or <see langword="null"/> when none is advertised.</returns>
    public static int? GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
        {
            return Math.Max(1, (int)Math.Ceiling(delta.TotalSeconds));
        }

        if (response.Headers.RetryAfter?.Date is DateTimeOffset date)
        {
            var seconds = (int)Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds);
            return Math.Max(1, seconds);
        }

        if (response.Headers.TryGetValues("Retry-After", out var values)
            && int.TryParse(values.FirstOrDefault(), out var retryAfterSeconds))
        {
            return retryAfterSeconds;
        }

        return null;
    }
}
