using ClientManager.StorageApi.Models.Exceptions;
using ClientManager.Shared.Logging;

namespace ClientManager.StorageApi.Middlewares;

/// <summary>
/// Converts unhandled exceptions into RFC 7807-style problem responses.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAppLogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        IAppLogger<ErrorHandlingMiddleware> logger)
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
        catch (StorageApiProblemException exception)
        {
            _logger.Warn("Storage API request failed", new
            {
                Path = context.Request.Path.Value,
                exception.ErrorCode,
                exception.StatusCode,
                Detail = exception.Message
            });

            if (exception.RetryAfterSeconds.HasValue)
            {
                context.Response.Headers.RetryAfter = exception.RetryAfterSeconds.Value.ToString();
            }

            await WriteProblemDetailsAsync(
                context,
                exception.StatusCode,
                exception.Title,
                exception.Message,
                exception.ErrorCode);
        }
        catch (InvalidOperationException exception)
        {
            _logger.Warn("Invalid request", new { Path = context.Request.Path.Value, Detail = exception.Message });
            await WriteProblemDetailsAsync(context, StatusCodes.Status400BadRequest, "Bad Request", exception.Message, errorCode: null);
        }
        catch (Exception exception)
        {
            _logger.Error("Unhandled exception", exception, new
            {
                Path = context.Request.Path.Value,
                context.Request.Method
            });

            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred.",
                errorCode: null);
        }
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string? errorCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            title,
            status = statusCode,
            detail,
            errorCode,
            traceId = context.TraceIdentifier
        });
    }
}