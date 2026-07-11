using ClientManager.Api.Models.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Filters;

/// <summary>
/// Returns HTTP 404 when seed HTTP endpoints are disabled via <see cref="DangerZoneOptions"/>.
/// </summary>
public sealed class SeedEndpointGateFilter(IOptions<DangerZoneOptions> options) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var dangerZone = options.Value;
        var method = context.HttpContext.Request.Method;

        var allowed = method switch
        {
            "GET" => dangerZone.IsSeedExportEnabled,
            "POST" or "PUT" or "DELETE" => dangerZone.IsSeedImportEnabled,
            _ => true
        };

        if (!allowed)
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
