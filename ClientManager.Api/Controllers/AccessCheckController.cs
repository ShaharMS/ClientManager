using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Problems;
using ClientManager.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Operational endpoints for checking whether a client may call a service right now.
/// </summary>
/// <remarks>
/// <para>
/// This controller is the runtime contract consumed by nginx <c>auth_request</c> (and similar gateways).
/// It evaluates client enablement, service enablement, per-client service access, and all configured
/// rate-limit scopes before traffic reaches upstream applications.
/// </para>
/// <para>
/// Denials use the same HTTP status codes and RFC 7807 problem bodies that operators already map in
/// reverse proxies. Problem responses also carry <c>X-Problem-*</c> headers so nginx can branch without
/// parsing JSON bodies.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/access")]
[Tags("Access Check")]
public class AccessCheckController(IAccessControlService accessControlService) : ControllerBase
{
    /// <summary>
    /// Checks if a client can access a service right now.
    /// </summary>
    /// <param name="request">Query parameters identifying the client and service.</param>
    /// <param name="cancellationToken">Token used to abort the access check before it completes.</param>
    /// <returns>The access check response with remaining request information.</returns>
    /// <response code="200">Access is granted.</response>
    /// <response code="401">No access configuration exists for the client-service relationship.</response>
    /// <response code="400">Unknown client identifier.</response>
    /// <response code="403">Access is denied because the client is disabled, the service is disabled, or the client-service relationship exists but is disabled.</response>
    /// <response code="404">Service not found.</response>
    /// <response code="429">Rate limit exceeded.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
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
