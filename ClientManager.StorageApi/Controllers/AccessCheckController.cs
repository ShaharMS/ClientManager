using Asp.Versioning;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Internal runtime endpoints for access checks.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/runtime/access")]
[Tags("Runtime Operations")]
public class AccessCheckController : ControllerBase
{
    private readonly IAccessControlService _accessControlService;

    /// <summary>
    /// Initializes a new instance of <see cref="AccessCheckController"/>.
    /// </summary>
    /// <param name="accessControlService">The runtime access-control service.</param>
    public AccessCheckController(IAccessControlService accessControlService)
    {
        _accessControlService = accessControlService;
    }

    /// <summary>
    /// Evaluates whether a client can access a service right now.
    /// </summary>
    /// <param name="request">The access-check request.</param>
    /// <param name="cancellationToken">Cancels the runtime access-check pipeline.</param>
    /// <response code="200">Returns the access decision.</response>
    /// <response code="401">The client has no configuration entry for the requested service.</response>
    /// <response code="403">The request is forbidden because the client, service, or access relationship is disabled.</response>
    /// <response code="404">The client or service does not exist.</response>
    /// <response code="429">A client or global rate limit was exceeded.</response>
    [HttpPost("check")]
    [ProducesResponseType(typeof(AccessCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CheckAccess([FromBody] CheckAccessRequest request, CancellationToken cancellationToken)
    {
        var response = await _accessControlService.CheckAccessAsync(request.ClientId, request.ServiceId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Returns the current accessibility summary for a client across all services.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancels the accessibility lookup.</param>
    /// <response code="200">Returns the client's accessibility summary.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("{clientId}")]
    [ProducesResponseType(typeof(ClientAccessibilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccessibility(string clientId, CancellationToken cancellationToken)
    {
        var response = await _accessControlService.GetClientAccessibilityAsync(clientId, cancellationToken);
        return Ok(response);
    }
}