using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages client-level global rate limit endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationGlobalRateLimitController : ControllerBase
{
    private readonly IClientGlobalRateLimitService _clientGlobalRateLimitService;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientConfigurationGlobalRateLimitController"/>.
    /// </summary>
    /// <param name="clientGlobalRateLimitService">The client global rate limit service.</param>
    public ClientConfigurationGlobalRateLimitController(IClientGlobalRateLimitService clientGlobalRateLimitService)
    {
        _clientGlobalRateLimitService = clientGlobalRateLimitService;
    }

    /// <summary>
    /// Gets the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The client's global rate limit.</returns>
    /// <response code="200">Returns the global rate limit.</response>
    /// <response code="404">Client not found or no global rate limit is configured.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("{id}/global-rate-limit")]
    [ProducesResponseType(typeof(ClientRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetGlobalRateLimit(string id, CancellationToken cancellationToken)
    {
        var rateLimit = await _clientGlobalRateLimitService.GetGlobalRateLimitAsync(id, cancellationToken);
        return Ok(rateLimit);
    }

    /// <summary>
    /// Sets the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="rateLimit">The global rate limit to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The global rate limit was set.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPut("{id}/global-rate-limit")]
    [ProducesResponseType(typeof(ClientRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetGlobalRateLimit(string id, [FromBody] ClientRateLimit rateLimit, CancellationToken cancellationToken)
    {
        var applied = await _clientGlobalRateLimitService.SetGlobalRateLimitAsync(id, rateLimit, cancellationToken);
        return Ok(applied);
    }

    /// <summary>
    /// Removes the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The global rate limit was removed.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpDelete("{id}/global-rate-limit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RemoveGlobalRateLimit(string id, CancellationToken cancellationToken)
    {
        await _clientGlobalRateLimitService.RemoveGlobalRateLimitAsync(id, cancellationToken);
        return NoContent();
    }
}