using ClientManager.Shared.Models.Requests;

using ClientManager.Shared.Models.Responses;

using ClientManager.Shared.Models.Problems;

using ClientManager.Api.Services.Interfaces;

using Microsoft.AspNetCore.Http;

using Microsoft.AspNetCore.Mvc;



namespace ClientManager.Api.Controllers;



/// <summary>Operational endpoints for checking client access to services.</summary>
[ApiController]

[Route("api/v1/access")]

[Tags("Access Check")]

public class AccessCheckController(IAccessControlService accessControlService) : ControllerBase

{

    /// <summary>Checks if a client can access a service right now.</summary>
    [HttpGet("check")]

    [ProducesResponseType(typeof(AccessCheckResponse), StatusCodes.Status200OK)]

    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]

    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status401Unauthorized)]

    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status403Forbidden)]

    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]

    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status429TooManyRequests)]

    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]

    public async Task<IActionResult> CheckAccess([FromQuery] CheckAccessRequest request, CancellationToken cancellationToken) =>

        Ok(await accessControlService.CheckAccessAsync(request.ClientId, request.ServiceId, cancellationToken));

}
