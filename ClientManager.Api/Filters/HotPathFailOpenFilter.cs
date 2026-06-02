using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Filters;

/// <summary>
/// Converts an unexpected server error on a hot-path action into a successful fail-open
/// response when the <c>HotPathResilience</c> option is enabled. Only actions annotated with
/// <see cref="FailOpenOnErrorAttribute"/> are affected.
/// <para>
/// Deliberate problem outcomes (<see cref="HttpProblemException"/>: 403/404/429/503, etc.) and
/// client-aborted requests are never masked — they continue to propagate so the existing
/// behavior is preserved. Only an otherwise-unhandled exception (the kind that would become a
/// 500) is suppressed, and the original failure is always logged at error level so it remains
/// visible to operators. See <see cref="HotPathResilienceOptions"/> for the trade-off.
/// </para>
/// </summary>
public sealed class HotPathFailOpenFilter : IAsyncActionFilter
{
    private readonly IOptionsMonitor<HotPathResilienceOptions> _options;
    private readonly IAppLogger<HotPathFailOpenFilter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="HotPathFailOpenFilter"/>.
    /// </summary>
    /// <param name="options">The hot-path resilience options, read per request so the switch can be toggled via configuration reload.</param>
    /// <param name="logger">Logger used to record suppressed failures so they remain visible to operators.</param>
    public HotPathFailOpenFilter(
        IOptionsMonitor<HotPathResilienceOptions> options,
        IAppLogger<HotPathFailOpenFilter> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failOpen = context.ActionDescriptor.EndpointMetadata
            .OfType<FailOpenOnErrorAttribute>()
            .FirstOrDefault();

        // Not a hot-path action, or the feature is disabled: behave exactly as before.
        if (failOpen is null || !_options.CurrentValue.FailOpenOnServerError)
        {
            await next();
            return;
        }

        var executed = await next();

        if (!ShouldSuppress(executed, context.HttpContext))
        {
            return;
        }

        var fallback = BuildFallback(failOpen.Kind, context.ActionArguments);
        if (fallback is null)
        {
            // No fallback could be built (unexpected argument shape); let the error surface.
            return;
        }

        _logger.Error(
            "Hot-path fail-open engaged: suppressing server error and returning a success response",
            new
            {
                Path = context.HttpContext.Request.Path.Value,
                context.HttpContext.Request.Method,
                failOpen.Kind
            },
            executed.Exception!);

        executed.Result = new OkObjectResult(fallback);
        executed.ExceptionHandled = true;
    }

    /// <summary>
    /// Determines whether the executed action's exception is an unexpected server error that
    /// should be converted into a fail-open success. Deliberate problem responses and
    /// client-aborted requests are left untouched.
    /// </summary>
    private static bool ShouldSuppress(ActionExecutedContext executed, HttpContext httpContext)
    {
        if (executed.Exception is null || executed.ExceptionHandled)
        {
            return false;
        }

        // Modeled, deliberate outcomes (403/404/429/503, ...) are part of the contract.
        if (executed.Exception is HttpProblemException)
        {
            return false;
        }

        // A genuine client disconnect should not be turned into a fabricated success.
        if (executed.Exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds the fail-open response body for the given hot path, echoing identifiers from the
    /// bound request so the client receives a well-formed, ordinary-looking success.
    /// </summary>
    private object? BuildFallback(HotPathFailOpenKind kind, IDictionary<string, object?> arguments)
    {
        switch (kind)
        {
            case HotPathFailOpenKind.GrantAccess
                when arguments.Values.OfType<CheckAccessRequest>().FirstOrDefault() is { } accessRequest:
                return new AccessCheckResponse
                {
                    ClientId = accessRequest.ClientId,
                    ServiceId = accessRequest.ServiceId,
                    RemainingRequests = null
                };

            case HotPathFailOpenKind.GrantAcquire
                when arguments.Values.OfType<AcquireResourceRequest>().FirstOrDefault() is not null:
                return new ResourceAcquireResponse
                {
                    AllocationId = Guid.NewGuid().ToString(),
                    ExpiresAt = DateTime.UtcNow.Add(_options.CurrentValue.FailOpenAllocationLifetime)
                };

            case HotPathFailOpenKind.ConfirmRelease:
                return new ResourceReleaseResponse { Released = true };

            default:
                return null;
        }
    }
}
