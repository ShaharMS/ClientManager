using Asp.Versioning;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
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
    private readonly IClientConfigurationStoreClient _clientConfigurationStoreClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientConfigurationServicesController"/>.
    /// </summary>
    /// <param name="clientConfigurationStoreClient">The internal configuration store client.</param>
    public ClientConfigurationServicesController(IClientConfigurationStoreClient clientConfigurationStoreClient)
    {
        _clientConfigurationStoreClient = clientConfigurationStoreClient;
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
        var result = await _clientConfigurationStoreClient.GetServicesAsync(id, paging, cancellationToken);
        return Ok(result);
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
        var settings = await _clientConfigurationStoreClient.GetServiceSettingsAsync(id, serviceId, cancellationToken)
            ?? throw new ServiceSettingsNotFoundException(serviceId, id);
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
    [HttpPut("{id}/services/{serviceId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetServiceSettings(string id, string serviceId, [FromBody] ServiceAccessSettings settings, CancellationToken cancellationToken)
    {
        await _clientConfigurationStoreClient.SetServiceSettingsAsync(id, serviceId, settings, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Removes service access settings for a specific service (revokes access).
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The service access settings were removed.</response>
    [HttpDelete("{id}/services/{serviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveServiceSettings(string id, string serviceId, CancellationToken cancellationToken)
    {
        await _clientConfigurationStoreClient.RemoveServiceSettingsAsync(id, serviceId, cancellationToken);
        return NoContent();
    }
}