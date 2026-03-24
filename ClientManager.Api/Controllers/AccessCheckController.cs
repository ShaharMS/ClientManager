using Asp.Versioning;
using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Requests;
using ClientManager.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Operational endpoints for checking client access to services.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/access")]
[Tags("Access Check")]
public class AccessCheckController : ControllerBase
{
    private readonly IAccessControlService _accessControlService;

    /// <summary>
    /// Initializes a new instance of <see cref="AccessCheckController"/>.
    /// </summary>
    /// <param name="accessControlService">The access control service.</param>
    public AccessCheckController(IAccessControlService accessControlService)
    {
        _accessControlService = accessControlService;
    }

    /// <summary>
    /// Checks if a client can access a service right now.
    /// </summary>
    /// <param name="request">The access check request containing client and service IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The access check response with remaining request information.</returns>
    /// <response code="200">Access is granted.</response>
    /// <response code="401">No access configuration exists for the client-service relationship.</response>
    /// <response code="403">Access is denied because the client is disabled, the service is disabled, or the client-service relationship exists but is disabled.</response>
    /// <response code="404">Client or service not found.</response>
    /// <response code="429">Rate limit exceeded.</response>
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
    /// Gets a full accessibility report for a client across all services.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The client accessibility report.</returns>
    /// <response code="200">Returns the accessibility report.</response>
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
