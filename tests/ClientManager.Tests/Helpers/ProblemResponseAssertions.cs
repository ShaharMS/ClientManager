using System.Net.Http.Headers;
using System.Text.Json;
using ClientManager.Shared.Models.Problems;

namespace ClientManager.Tests.Helpers;

public static class ProblemResponseAssertions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task AssertProblemAsync(
        HttpResponseMessage response,
        int expectedStatus,
        bool expectRetryAfter = false)
    {
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemResponse>(body, JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(expectedStatus, problem.Status);

        Assert.True(response.Headers.Contains(ProblemResponseHeaders.Json));
        Assert.True(response.Headers.Contains(ProblemResponseHeaders.Title));
        Assert.True(response.Headers.Contains(ProblemResponseHeaders.Detail));

        var headerJson = response.Headers.GetValues(ProblemResponseHeaders.Json).First();
        Assert.Equal(body, headerJson);

        if (expectRetryAfter)
        {
            Assert.True(response.Headers.RetryAfter is not null);
            Assert.False(string.IsNullOrWhiteSpace(response.Headers.RetryAfter.ToString()));
        }
    }
}
