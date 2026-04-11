using System.Net;
using System.Net.Http.Json;
using ClientManager.Shared.Models.Problems;

namespace ClientManager.AdminUI.Services;

internal static class ApiResponseHandler
{
    public static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw await CreateExceptionAsync(response);
    }

    public static async Task<T?> GetFromJsonAsync<T>(HttpClient httpClient, string path)
    {
        var response = await httpClient.GetAsync(path);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public static async Task<T?> GetOptionalFromJsonAsync<T>(HttpClient httpClient, string path)
    {
        var response = await httpClient.GetAsync(path);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private static async Task<Exception> CreateExceptionAsync(HttpResponseMessage response)
    {
        var message = await BuildMessageAsync(response);
        return new HttpRequestException(message, null, response.StatusCode);
    }

    private static async Task<string> BuildMessageAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return CreateUnavailableMessage(response);
        }

        var problem = await ReadProblemAsync(response);
        return problem.Detail
            ?? problem.Title
            ?? $"The API returned status {(int)response.StatusCode}.";
    }

    private static string CreateUnavailableMessage(HttpResponseMessage response)
    {
        var retryAfterSeconds = GetRetryAfterSeconds(response);
        return retryAfterSeconds is int seconds
            ? $"The service is temporarily unavailable. Try again in {seconds} seconds."
            : "The service is temporarily unavailable. Please try again shortly.";
    }

    private static async Task<ProblemResponse> ReadProblemAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<ProblemResponse>()
            ?? new ProblemResponse
            {
                Status = (int)response.StatusCode,
                Detail = $"The API returned status {(int)response.StatusCode}."
            };
    }

    private static int? GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
        {
            return Math.Max(1, (int)Math.Ceiling(delta.TotalSeconds));
        }

        if (response.Headers.TryGetValues("Retry-After", out var values)
            && int.TryParse(values.FirstOrDefault(), out var retryAfterSeconds))
        {
            return retryAfterSeconds;
        }

        return null;
    }
}