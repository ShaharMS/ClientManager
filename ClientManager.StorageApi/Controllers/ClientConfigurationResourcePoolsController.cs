using Asp.Versioning;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Manages internal per-resource-pool client configuration endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/configuration/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationResourcePoolsController : ControllerBase
{
    private readonly IClientConfigurationCatalogService _service;

    public ClientConfigurationResourcePoolsController(IClientConfigurationCatalogService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lists all resource pool settings for a client, paginated.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="paging">Pagination parameters.</param>
    /// <response code="200">Returns the paginated resource pool settings.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("{id}/resource-pools")]
    [ProducesResponseType(typeof(PagedResponse<KeyedEntry<ResourcePoolSettings>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourcePools(string id, [FromQuery] PagedRequest paging, CancellationToken cancellationToken)
    {
        var result = await _service.GetResourcePoolsAsync(id, paging, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Gets resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <response code="200">Returns the resource pool settings or null when no entry exists.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(typeof(ResourcePoolSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourcePoolSettings(string id, string poolId, CancellationToken cancellationToken)
    {
        var result = await _service.GetResourcePoolSettingsAsync(id, poolId, cancellationToken);
        return !result.ClientExists ? NotFound() : Ok(result.Value);
    }

    /// <summary>
    /// Creates or updates resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="settings">The resource pool settings to apply.</param>
    /// <response code="200">The resource pool settings were updated.</response>
    [HttpPut("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetResourcePoolSettings(string id, string poolId, [FromBody] ResourcePoolSettings settings, CancellationToken cancellationToken)
    {
        await _service.SetResourcePoolSettingsAsync(id, poolId, settings, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Removes resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <response code="204">The resource pool settings were removed.</response>
    [HttpDelete("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveResourcePoolSettings(string id, string poolId, CancellationToken cancellationToken)
    {
        await _service.RemoveResourcePoolSettingsAsync(id, poolId, cancellationToken);
        return NoContent();
    }
}