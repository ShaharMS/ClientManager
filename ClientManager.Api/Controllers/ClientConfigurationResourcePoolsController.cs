using Asp.Versioning;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages per-resource-pool client configuration endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationResourcePoolsController : ControllerBase
{
    private readonly IClientConfigurationStoreClient _clientConfigurationStoreClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientConfigurationResourcePoolsController"/>.
    /// </summary>
    /// <param name="clientConfigurationStoreClient">The internal configuration store client.</param>
    public ClientConfigurationResourcePoolsController(IClientConfigurationStoreClient clientConfigurationStoreClient)
    {
        _clientConfigurationStoreClient = clientConfigurationStoreClient;
    }

    /// <summary>
    /// Lists all resource pool settings for a client, paginated.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="paging">Pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of resource pool setting entries.</returns>
    /// <response code="200">Returns the paginated resource pool settings.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("{id}/resource-pools")]
    [ProducesResponseType(typeof(PagedResponse<KeyedEntry<ResourcePoolSettings>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourcePools(string id, [FromQuery] PagedRequest paging, CancellationToken cancellationToken)
    {
        var result = await _clientConfigurationStoreClient.GetResourcePoolsAsync(id, paging, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resource pool settings.</returns>
    /// <response code="200">Returns the resource pool settings.</response>
    /// <response code="404">Client or pool settings not found.</response>
    [HttpGet("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(typeof(ResourcePoolSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourcePoolSettings(string id, string poolId, CancellationToken cancellationToken)
    {
        var settings = await _clientConfigurationStoreClient.GetResourcePoolSettingsAsync(id, poolId, cancellationToken)
            ?? throw new ResourcePoolSettingsNotFoundException(poolId, id);
        return Ok(settings);
    }

    /// <summary>
    /// Creates or updates resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="settings">The resource pool settings to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The resource pool settings were updated.</response>
    [HttpPut("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetResourcePoolSettings(string id, string poolId, [FromBody] ResourcePoolSettings settings, CancellationToken cancellationToken)
    {
        await _clientConfigurationStoreClient.SetResourcePoolSettingsAsync(id, poolId, settings, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Removes resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The resource pool settings were removed.</response>
    [HttpDelete("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveResourcePoolSettings(string id, string poolId, CancellationToken cancellationToken)
    {
        await _clientConfigurationStoreClient.RemoveResourcePoolSettingsAsync(id, poolId, cancellationToken);
        return NoContent();
    }
}