using ClientManager.Api.Models.Exceptions;

namespace ClientManager.Api.Middleware;

/// <summary>
/// Catches typed exceptions thrown by services and maps them to HTTP responses
/// using the RFC 7807 Problem Details format.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
            _logger.LogWarning("Resource not found | Path={Path}, Detail={Detail}",
                context.Request.Path.Value, exception.Message);
            await WriteProblemDetailsAsync(context, StatusCodes.Status404NotFound, "Not Found", exception.Message);
        }
        catch (ConflictException exception)
        {
            _logger.LogWarning("Conflict | Path={Path}, Detail={Detail}",
                context.Request.Path.Value, exception.Message);
            await WriteProblemDetailsAsync(context, StatusCodes.Status409Conflict, "Conflict", exception.Message);
        }
        catch (ValidationException exception)
        {
            _logger.LogWarning("Validation failed | Path={Path}, Detail={Detail}",
                context.Request.Path.Value, exception.Message);
            await WriteProblemDetailsAsync(context, StatusCodes.Status400BadRequest, "Bad Request", exception.Message);
        }
        catch (AccessDeniedException exception)
        {
            _logger.LogWarning("Access denied | Path={Path}, ClientId={ClientId}, ServiceId={ServiceId}",
                context.Request.Path.Value, exception.ClientId, exception.ServiceId);
            await WriteProblemDetailsAsync(context, StatusCodes.Status403Forbidden, "Forbidden", exception.Message);
        }
        catch (ClientDisabledException exception)
        {
            _logger.LogWarning("Client disabled | Path={Path}, ClientId={ClientId}",
                context.Request.Path.Value, exception.ClientId);
            await WriteProblemDetailsAsync(context, StatusCodes.Status403Forbidden, "Forbidden", exception.Message);
        }
        catch (RateLimitedException exception)
        {
            _logger.LogWarning("Rate limited | Path={Path}, Detail={Detail}, RetryAfterSeconds={RetryAfterSeconds}",
                context.Request.Path.Value, exception.Message, exception.RetryAfterSeconds);

            if (exception.RetryAfterSeconds.HasValue)
            {
                context.Response.Headers.RetryAfter = exception.RetryAfterSeconds.Value.ToString();
            }

            await WriteProblemDetailsAsync(context, StatusCodes.Status429TooManyRequests, "Too Many Requests", exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception | Path={Path}, Method={Method}",
                context.Request.Path.Value, context.Request.Method);
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
            type = $"https://httpstatuses.com/{statusCode}",
            title,
            status = statusCode,
            detail,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
