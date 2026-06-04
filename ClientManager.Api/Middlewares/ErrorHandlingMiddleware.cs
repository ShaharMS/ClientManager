using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Storage.Models.Exceptions;
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
public class ErrorHandlingMiddleware(
    RequestDelegate next,
    IAppLogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpProblemException exception)
        {
            await HandleProblemAsync(context, exception);
        }
        catch (StorageApiProblemException exception)
        {
            await HandleStorageProblemAsync(context, exception);
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

    private async Task HandleStorageProblemAsync(HttpContext context, StorageApiProblemException exception)
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

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
