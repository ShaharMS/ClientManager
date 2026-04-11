using Asp.Versioning;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.StorageApi.Models.Exceptions;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Manages internal per-service client configuration endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/configuration/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationServicesController : ControllerBase
{
    private readonly IClientConfigurationCatalogService _service;

    public ClientConfigurationServicesController(IClientConfigurationCatalogService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lists all service access settings for a client, paginated.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="paging">Pagination parameters.</param>
    /// <response code="200">Returns the paginated service access settings.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("{id}/services")]
    [ProducesResponseType(typeof(PagedResponse<KeyedEntry<ServiceAccessSettings>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServices(string id, [FromQuery] PagedRequest paging, CancellationToken cancellationToken)
    {
        var result = await _service.GetServicesAsync(id, paging, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Gets service access settings for a specific service.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <response code="200">Returns the service access settings.</response>
    /// <response code="404">No client or matching service settings were found.</response>
    [HttpGet("{id}/services/{serviceId}")]
    [ProducesResponseType(typeof(ServiceAccessSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceSettings(string id, string serviceId, CancellationToken cancellationToken)
    {
        var result = await _service.GetServiceSettingsAsync(id, serviceId, cancellationToken);
        if (!result.ClientExists)
        {
            throw new ClientNotFoundException(id);
        }

        if (result.Value is null)
        {
            throw new ServiceSettingsNotFoundException(id, serviceId);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Creates or updates service access settings for a specific service.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="settings">The service access settings to apply.</param>
    /// <response code="200">The service access settings were updated.</response>
    [HttpPut("{id}/services/{serviceId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetServiceSettings(string id, string serviceId, [FromBody] ServiceAccessSettings settings, CancellationToken cancellationToken)
    {
        await _service.SetServiceSettingsAsync(id, serviceId, settings, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Removes service access settings for a specific service.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <response code="204">The service access settings were removed.</response>
    [HttpDelete("{id}/services/{serviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveServiceSettings(string id, string serviceId, CancellationToken cancellationToken)
    {
        await _service.RemoveServiceSettingsAsync(id, serviceId, cancellationToken);
        return NoContent();
    }
}