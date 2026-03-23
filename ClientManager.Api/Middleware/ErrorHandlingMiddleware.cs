using ClientManager.Api.Models.Exceptions;
using ClientManager.Shared.Logging;

namespace ClientManager.Api.Middleware;

/// <summary>
/// Catches typed exceptions thrown by services and maps them to HTTP responses
/// using the RFC 7807 Problem Details format.
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
        catch (NotFoundException exception)
        {
            _logger.Warn("Resource not found", new { Path = context.Request.Path.Value, Detail = exception.Message });
            await WriteProblemDetailsAsync(context, StatusCodes.Status404NotFound, "Not Found", exception.Message);
        }
        catch (ConflictException exception)
        {
            _logger.Warn("Conflict", new { Path = context.Request.Path.Value, Detail = exception.Message });
            await WriteProblemDetailsAsync(context, StatusCodes.Status409Conflict, "Conflict", exception.Message);
        }
        catch (ValidationException exception)
        {
            _logger.Warn("Validation failed", new { Path = context.Request.Path.Value, Detail = exception.Message });
            await WriteProblemDetailsAsync(context, StatusCodes.Status400BadRequest, "Bad Request", exception.Message);
        }
        catch (AccessDeniedException exception)
        {
            _logger.Warn("Access denied", new { Path = context.Request.Path.Value, ClientId = exception.ClientId, ServiceId = exception.ServiceId });
            await WriteProblemDetailsAsync(context, StatusCodes.Status403Forbidden, "Forbidden", exception.Message);
        }
        catch (ClientDisabledException exception)
        {
            _logger.Warn("Client disabled", new { Path = context.Request.Path.Value, ClientId = exception.ClientId });
            await WriteProblemDetailsAsync(context, StatusCodes.Status403Forbidden, "Forbidden", exception.Message);
        }
        catch (RateLimitedException exception)
        {
            _logger.Warn("Rate limited", new { Path = context.Request.Path.Value, Detail = exception.Message, RetryAfterSeconds = exception.RetryAfterSeconds });

            if (exception.RetryAfterSeconds.HasValue)
            {
                context.Response.Headers.RetryAfter = exception.RetryAfterSeconds.Value.ToString();
            }

            await WriteProblemDetailsAsync(context, StatusCodes.Status429TooManyRequests, "Too Many Requests", exception.Message);
        }
        catch (Exception exception)
        {
            _logger.Error("Unhandled exception", exception, new { Path = context.Request.Path.Value, Method = context.Request.Method });
            await WriteProblemDetailsAsync(context, StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new
        {
            title,
            status = statusCode,
            detail,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
