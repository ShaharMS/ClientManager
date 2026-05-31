using ClientManager.Api.Models.Exceptions;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Logging;

namespace ClientManager.Api.Middlewares;

/// <summary>
/// Catches exceptions thrown while handling a request and maps them to HTTP responses using
/// the RFC 7807 Problem Details format. Expected failures derive from
/// <see cref="HttpProblemException"/> and carry their own status code, title, and retry hint, so
/// they are translated and logged at warning level through a single path. Any other exception is
/// treated as an unexpected defect and surfaced as a 500 with an error-level log.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAppLogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, IAppLogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpProblemException exception)
        {
            await HandleProblemAsync(context, exception);
        }
        catch (Exception exception)
        {
            _logger.Error("Unhandled exception", exception, new { Path = context.Request.Path.Value, context.Request.Method });
            await WriteProblemDetailsAsync(context, StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Writes the RFC 7807 response for an expected failure. Expected failures are logged at
    /// warning level — never error level — because they are part of the public contract rather
    /// than defects, and the <c>Retry-After</c> header is preserved for throttled or unavailable
    /// responses that carry a retry hint.
    /// </summary>
    private async Task HandleProblemAsync(HttpContext context, HttpProblemException exception)
    {
        _logger.Warn(
            "Request failed with a handled problem",
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

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
