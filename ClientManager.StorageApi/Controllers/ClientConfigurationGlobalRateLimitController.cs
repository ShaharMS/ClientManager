using Asp.Versioning;
using ClientManager.Shared.Models.Entities;
using ClientManager.StorageApi.Models.Exceptions;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Manages internal client-level global rate-limit endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/configuration/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationGlobalRateLimitController : ControllerBase
{
    private readonly IClientConfigurationCatalogService _service;

    public ClientConfigurationGlobalRateLimitController(IClientConfigurationCatalogService service)
    {
        _service = service;
    }

    /// <summary>
    /// Gets the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <response code="200">Returns the global rate limit.</response>
    /// <response code="404">No client or configured client rate limit was found.</response>
    [HttpGet("{id}/global-rate-limit")]
    [ProducesResponseType(typeof(ClientRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGlobalRateLimit(string id, CancellationToken cancellationToken)
    {
        var result = await _service.GetGlobalRateLimitAsync(id, cancellationToken);
        if (!result.ClientExists)
        {
            throw new ClientNotFoundException(id);
        }

        if (result.Value is null)
        {
            throw new ClientGlobalRateLimitNotFoundException(id);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sets the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="rateLimit">The global rate limit to apply.</param>
    /// <response code="200">The global rate limit was set.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpPut("{id}/global-rate-limit")]
    [ProducesResponseType(typeof(ClientRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetGlobalRateLimit(string id, [FromBody] ClientRateLimit rateLimit, CancellationToken cancellationToken)
    {
        var updated = await _service.SetGlobalRateLimitAsync(id, rateLimit, cancellationToken);
        if (!updated)
        {
            throw new ClientNotFoundException(id);
        }

        return Ok(rateLimit);
    }

    /// <summary>
    /// Removes the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <response code="204">The global rate limit was removed.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpDelete("{id}/global-rate-limit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveGlobalRateLimit(string id, CancellationToken cancellationToken)
    {
        var removed = await _service.RemoveGlobalRateLimitAsync(id, cancellationToken);
        if (!removed)
        {
            throw new ClientNotFoundException(id);
        }

        return NoContent();
    }
}