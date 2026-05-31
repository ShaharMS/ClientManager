using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages per-service client configuration endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationServicesController : ControllerBase
{
    private readonly IClientServiceSettingsService _clientServiceSettingsService;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientConfigurationServicesController"/>.
    /// </summary>
    /// <param name="clientServiceSettingsService">The client service-settings service.</param>
    public ClientConfigurationServicesController(IClientServiceSettingsService clientServiceSettingsService)
    {
        _clientServiceSettingsService = clientServiceSettingsService;
    }

    /// <summary>
    /// Lists all service access settings for a client, paginated.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="paging">Pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of service access setting entries.</returns>
    /// <response code="200">Returns the paginated service access settings.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("{id}/services")]
    [ProducesResponseType(typeof(PagedResponse<KeyedEntry<ServiceAccessSettings>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServices(string id, [FromQuery] PagedRequest paging, CancellationToken cancellationToken)
    {
        var settings = await _clientServiceSettingsService.GetServicesAsync(id, paging, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Gets service access settings for a specific service.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The service access settings.</returns>
    /// <response code="200">Returns the service access settings.</response>
    /// <response code="404">Client or service settings not found.</response>
    [HttpGet("{id}/services/{serviceId}")]
    [ProducesResponseType(typeof(ServiceAccessSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceSettings(string id, string serviceId, CancellationToken cancellationToken)
    {
        var settings = await _clientServiceSettingsService.GetServiceSettingsAsync(id, serviceId, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Creates or updates service access settings for a specific service.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="settings">The service access settings to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The service access settings were updated.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpPut("{id}/services/{serviceId}")]
    [ProducesResponseType(typeof(ServiceAccessSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetServiceSettings(string id, string serviceId, [FromBody] ServiceAccessSettings settings, CancellationToken cancellationToken)
    {
        var applied = await _clientServiceSettingsService.SetServiceSettingsAsync(id, serviceId, settings, cancellationToken);
        return Ok(applied);
    }

    /// <summary>
    /// Removes service access settings for a specific service (revokes access).
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The service access settings were removed.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpDelete("{id}/services/{serviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveServiceSettings(string id, string serviceId, CancellationToken cancellationToken)
    {
        await _clientServiceSettingsService.RemoveServiceSettingsAsync(id, serviceId, cancellationToken);
        return NoContent();
    }
}