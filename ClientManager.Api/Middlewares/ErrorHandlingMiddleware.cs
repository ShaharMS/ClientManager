using System.Text.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Problems;

namespace ClientManager.Api.Middlewares;

/// <summary>
/// Catches exceptions thrown while handling a request and maps them to HTTP responses using
/// the RFC 7807 Problem Details format. Expected failures derive from
/// <see cref="HttpProblemException"/> and carry their own status code, title, and retry hint, so
/// they are translated and logged at warning level through a single path. Any other exception is
/// treated as an unexpected defect and surfaced as a 500 with an error-level log.
/// </summary>
public class ErrorHandlingMiddleware(
    RequestDelegate next,
    IAppLogger<ErrorHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions ProblemJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // This exception means the use r probably navigated away/terminated the connection
            // We don't want to accidentally log this as an error, so we just rethrow it
            // To let Kestrel handle it gracefully (Kestrel is the underlying web server of ASP.NET)
            throw;
        }
        catch (HttpProblemException exception)
        {
            await HandleProblemAsync(context, exception);
        }
        catch (Exception exception)
        {
            logger.Error("Internal error occured while processing request", new { Path = context.Request.Path.Value, context.Request.Method }, exception);
            await WriteProblemDetailsAsync(context, StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.");
        }
    }

    private async Task HandleProblemAsync(HttpContext context, HttpProblemException exception)
    {
        logger.Info(
            "User fault encountered while processing request",
            new
            {
                Path = context.Request.Path.Value,
                exception.StatusCode,
                exception.Title,
                Detail = exception.Message,
                exception.RetryAfterSeconds
            });

        if (exception.RetryAfterSeconds.HasValue)
        {
            context.Response.Headers.RetryAfter = exception.RetryAfterSeconds.Value.ToString();
        }

        await WriteProblemDetailsAsync(context, exception.StatusCode, exception.Title, exception.Message);
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemResponse
        {
            Title = title,
            Status = statusCode,
            Detail = detail,
            TraceId = context.TraceIdentifier
        };

        var json = JsonSerializer.Serialize(problemDetails, ProblemJsonOptions);
        WriteProblemHeaders(context.Response.Headers, problemDetails, json);

        await context.Response.WriteAsync(json);
    }

    private static void WriteProblemHeaders(
        IHeaderDictionary headers,
        ProblemResponse problem,
        string json)
    {
        headers[ProblemResponseHeaders.Json] = json;

        if (!string.IsNullOrEmpty(problem.Title))
        {
            headers[ProblemResponseHeaders.Title] = SanitizeHeaderValue(problem.Title);
        }

        if (!string.IsNullOrEmpty(problem.Detail))
        {
            headers[ProblemResponseHeaders.Detail] = SanitizeHeaderValue(problem.Detail);
        }

        if (!string.IsNullOrEmpty(problem.TraceId))
        {
            headers[ProblemResponseHeaders.TraceId] = problem.TraceId;
        }
    }

    private static string SanitizeHeaderValue(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
