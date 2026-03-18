# Plan: ClientManager — Step 9: Middlewares

> **Status**: ✅ Completed
> **Prerequisite**: [client-manager-8-statistics-collection.md](client-manager-8-statistics-collection.md)
> **Next**: [client-manager-10-statistics-endpoints.md](client-manager-10-statistics-endpoints.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Create two ASP.NET Core middlewares: `RequestTrackingMiddleware` (outermost — wraps everything, starts Activity, records request/response metrics and latency) and `ErrorHandlingMiddleware` (inner — catches typed exceptions thrown by services and maps them to appropriate HTTP status codes and problem details responses). Pipeline order: RequestTracking → ErrorHandling → Controllers.

## Reference Pattern

ASP.NET Core middleware convention:

```csharp
public class ExampleMiddleware
{
    private readonly RequestDelegate _next;

    public ExampleMiddleware(RequestDelegate next) { ... }

    public async Task InvokeAsync(HttpContext context)
    {
        // before
        await _next(context);
        // after
    }
}
```

Registered in `Program.cs` via `app.UseMiddleware<T>()`.

## Steps

### 1. Create `RequestTrackingMiddleware`

**File: `ClientManager.Api/Middleware/RequestTrackingMiddleware.cs`**

This is the outermost custom middleware. It wraps the entire request pipeline to capture timing, log request/response pairs, and record OpenTelemetry metrics.

```csharp
using System.Diagnostics;
using ClientManager.Api.Services.Instrumentation;

namespace ClientManager.Api.Middleware;

public class RequestTrackingMiddleware
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly RequestDelegate _next;

    public RequestTrackingMiddleware(RequestDelegate next) { ... }

    public async Task InvokeAsync(HttpContext context, ClientManagerMetrics metrics)
    {
        var stopwatch = Stopwatch.StartNew();
        var activity = Activity.Current;

        Logger.Info("Request started | {@Properties}", new
        {
            TraceId = activity?.TraceId.ToString(),
            Method = context.Request.Method,
            Path = context.Request.Path.Value,
            QueryString = context.Request.QueryString.Value
        });

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;

            metrics.RequestsTotal.Add(1, new TagList
            {
                { "method", context.Request.Method },
                { "endpoint", context.Request.Path.Value },
                { "statusCode", statusCode.ToString() }
            });

            metrics.RequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
            {
                { "method", context.Request.Method },
                { "endpoint", context.Request.Path.Value }
            });

            if (statusCode >= 400)
            {
                metrics.RequestErrors.Add(1, new TagList
                {
                    { "method", context.Request.Method },
                    { "endpoint", context.Request.Path.Value },
                    { "statusCode", statusCode.ToString() }
                });
            }

            Logger.Info("Request completed | {@Properties}", new
            {
                TraceId = activity?.TraceId.ToString(),
                Method = context.Request.Method,
                Path = context.Request.Path.Value,
                StatusCode = statusCode,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds
            });
        }
    }
}
```

### 2. Create `ErrorHandlingMiddleware`

**File: `ClientManager.Api/Middleware/ErrorHandlingMiddleware.cs`**

Catches typed exceptions thrown by services and controllers and maps them to HTTP responses using the RFC 7807 Problem Details format.

```csharp
using System.Text.Json;
using ClientManager.Api.Models.Exceptions;

namespace ClientManager.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly RequestDelegate _next;

    public ErrorHandlingMiddleware(RequestDelegate next) { ... }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException exception)
        {
            Logger.Warn("Resource not found | {@Properties}", new
            {
                Path = context.Request.Path.Value,
                Detail = exception.Message
            });
            await WriteProblemDetailsAsync(context, StatusCodes.Status404NotFound, "Not Found", exception.Message);
        }
        catch (ConflictException exception)
        {
            Logger.Warn("Conflict | {@Properties}", new
            {
                Path = context.Request.Path.Value,
                Detail = exception.Message
            });
            await WriteProblemDetailsAsync(context, StatusCodes.Status409Conflict, "Conflict", exception.Message);
        }
        catch (ValidationException exception)
        {
            Logger.Warn("Validation failed | {@Properties}", new
            {
                Path = context.Request.Path.Value,
                Detail = exception.Message
            });
            await WriteProblemDetailsAsync(context, StatusCodes.Status400BadRequest, "Bad Request", exception.Message);
        }
        catch (AccessDeniedException exception)
        {
            Logger.Warn("Access denied | {@Properties}", new
            {
                Path = context.Request.Path.Value,
                ClientId = exception.ClientId,
                ServiceId = exception.ServiceId
            });
            await WriteProblemDetailsAsync(context, StatusCodes.Status403Forbidden, "Forbidden", exception.Message);
        }
        catch (ClientDisabledException exception)
        {
            Logger.Warn("Client disabled | {@Properties}", new
            {
                Path = context.Request.Path.Value,
                ClientId = exception.ClientId
            });
            await WriteProblemDetailsAsync(context, StatusCodes.Status403Forbidden, "Forbidden", exception.Message);
        }
        catch (RateLimitedException exception)
        {
            Logger.Warn("Rate limited | {@Properties}", new
            {
                Path = context.Request.Path.Value,
                Detail = exception.Message,
                RetryAfterSeconds = exception.RetryAfterSeconds
            });

            if (exception.RetryAfterSeconds.HasValue)
            {
                context.Response.Headers.RetryAfter = exception.RetryAfterSeconds.Value.ToString();
            }

            await WriteProblemDetailsAsync(context, StatusCodes.Status429TooManyRequests, "Too Many Requests", exception.Message);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Unhandled exception | {@Properties}", new
            {
                Path = context.Request.Path.Value,
                Method = context.Request.Method
            });
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
```

### 3. Register middleware in the pipeline

**File: `ClientManager.Api/Program.cs`** (documented here, wired in step 11)

The order is critical — `RequestTrackingMiddleware` must be outermost so it captures the full latency including error handling:

```csharp
app.UseMiddleware<RequestTrackingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();

// ... existing middleware (routing, auth, etc.)

app.MapControllers();
```

### 4. Define `ClientDisabledException` with `ClientId` property

This exception type was defined in step 1 but needs a `ClientId` property for the middleware to log:

```csharp
namespace ClientManager.Api.Models.Exceptions;

public class ClientDisabledException : Exception
{
    public string ClientId { get; }

    public ClientDisabledException(string clientId)
        : base($"Client '{clientId}' is disabled")
    {
        ClientId = clientId;
    }
}
```

> This was already specified in step 1 — listed here for reference so the implementing agent knows the shape the middleware depends on.

## Verification

- `dotnet build` succeeds
- `RequestTrackingMiddleware` logs request start and completion with trace IDs and duration
- `RequestTrackingMiddleware` records `RequestsTotal`, `RequestDuration`, and `RequestErrors` metrics
- `ErrorHandlingMiddleware` returns 404 for `NotFoundException`
- `ErrorHandlingMiddleware` returns 409 for `ConflictException`
- `ErrorHandlingMiddleware` returns 400 for `ValidationException`
- `ErrorHandlingMiddleware` returns 403 for `ClientDisabledException`
- `ErrorHandlingMiddleware` returns 403 for `AccessDeniedException`
- `ErrorHandlingMiddleware` returns 429 for `RateLimitedException` with `Retry-After` header
- `ErrorHandlingMiddleware` returns 500 for unhandled exceptions with a generic message (no internal details leaked)
- All error responses follow problem details format with `traceId`
- Pipeline order: RequestTracking → ErrorHandling → Controllers
