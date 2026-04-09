using Asp.Versioning;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
using ClientManager.Shared.Models.Entities;
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
    private readonly IClientConfigurationStoreClient _clientConfigurationStoreClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientConfigurationGlobalRateLimitController"/>.
    /// </summary>
    /// <param name="clientConfigurationStoreClient">The internal configuration store client.</param>
    public ClientConfigurationGlobalRateLimitController(IClientConfigurationStoreClient clientConfigurationStoreClient)
    {
        _clientConfigurationStoreClient = clientConfigurationStoreClient;
    }

    /// <summary>
    /// Gets the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The client's global rate limit.</returns>
    /// <response code="200">Returns the global rate limit.</response>
    /// <response code="404">Client not found or no global rate limit is configured.</response>
    [HttpGet("{id}/global-rate-limit")]
    [ProducesResponseType(typeof(ClientRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGlobalRateLimit(string id, CancellationToken cancellationToken)
    {
        var rateLimit = await _clientConfigurationStoreClient.GetGlobalRateLimitAsync(id, cancellationToken)
            ?? throw new ClientGlobalRateLimitNotFoundException(id);
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
    [HttpPut("{id}/global-rate-limit")]
    [ProducesResponseType(typeof(ClientRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetGlobalRateLimit(string id, [FromBody] ClientRateLimit rateLimit, CancellationToken cancellationToken)
    {
        await _clientConfigurationStoreClient.SetGlobalRateLimitAsync(id, rateLimit, cancellationToken);
        return Ok(rateLimit);
    }

    /// <summary>
    /// Removes the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The global rate limit was removed.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpDelete("{id}/global-rate-limit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveGlobalRateLimit(string id, CancellationToken cancellationToken)
    {
        await _clientConfigurationStoreClient.RemoveGlobalRateLimitAsync(id, cancellationToken);
        return NoContent();
    }
}