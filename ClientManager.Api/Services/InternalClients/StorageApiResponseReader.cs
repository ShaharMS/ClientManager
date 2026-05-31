using ClientManager.Shared.Models.Problems;

namespace ClientManager.Api.Services.InternalClients;

// CR: This classes is missing documentation, and its naming is a bit vauge - i dont mind not changing naming, but the methods definitely need documentation.

internal static class StorageApiResponseReader
{
    public static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        // CR: What is `emptyMessage`? naming is not clear - is this just a message for the error? name it more appropriately
        string emptyMessage)
    {
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException(emptyMessage);
    }

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

    public static Exception CreateUnexpectedException(
        HttpResponseMessage response,
        StorageProblemResponse problem)
    {
        var detail = problem.Detail ?? $"The storage API returned status {(int)response.StatusCode}.";
        return new HttpRequestException(detail, null, response.StatusCode);
    }

    public static async Task<Exception> CreateUnexpectedExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var problem = await ReadProblemAsync(response, cancellationToken);
        return CreateUnexpectedException(response, problem);
    }

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