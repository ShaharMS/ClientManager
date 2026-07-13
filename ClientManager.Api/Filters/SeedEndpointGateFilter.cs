using ClientManager.Shared.Configuration.Storage;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Mvc.Filters;

using Microsoft.Extensions.Options;



namespace ClientManager.Api.Filters;



/// <summary>Returns HTTP 404 when seed HTTP endpoints are disabled via <see cref="SeedOptions.SeedApiEnabled"/>.</summary>
public sealed class SeedEndpointGateFilter(IOptions<SeedOptions> options) : IAsyncActionFilter

{

    /// <inheritdoc />

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)

    {

        if (!options.Value.SeedApiEnabled)

        {

            context.Result = new NotFoundResult();

            return;

        }



        await next();

    }

}
